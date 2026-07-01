namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class RuntimeInteractionController(RuntimeStateStore stateStore, Action<string> requestStop)
{
    private readonly RuntimeStateStore _stateStore = stateStore;
    private readonly Action<string> _requestStop = requestStop;

    public void ApplyIntent(UiInputIntent intent)
    {
        switch (intent.Kind)
        {
            case UiInputIntentKind.MovePrevious:
                _stateStore.MoveScreen(-1);
                break;
            case UiInputIntentKind.MoveNext:
                _stateStore.MoveScreen(1);
                break;
            case UiInputIntentKind.MoveUp:
                _stateStore.MoveFocus(-1);
                break;
            case UiInputIntentKind.MoveDown:
                _stateStore.MoveFocus(1);
                break;
            case UiInputIntentKind.Activate:
                ActivateFocused();
                break;
            case UiInputIntentKind.Back:
                HandleBack();
                break;
            case UiInputIntentKind.Backspace:
                _stateStore.TryBackspaceCalibrationManualInput();
                break;
            case UiInputIntentKind.AppendDigit:
                if (intent.Digit.HasValue)
                {
                    _stateStore.TryAppendCalibrationManualDigit(intent.Digit.Value);
                }

                break;
        }
    }

    public void ActivateFocused()
    {
        switch (_stateStore.GetActiveScreen())
        {
            case RuntimeScreen.Overview:
                ActivateOverviewAction(_stateStore.GetFocusedOverviewAction());
                break;
            case RuntimeScreen.Calibration:
                ActivateCalibrationAction(_stateStore.GetFocusedCalibrationAction());
                break;
        }
    }

    public void ActivateOverviewAction(OverviewAction action)
    {
        switch (action)
        {
            case OverviewAction.AutoMode:
                _stateStore.SetBrightnessControlMode(BrightnessControlMode.Auto);
                _stateStore.AddEvent("Auto brightness mode enabled.", RuntimeEventSeverity.Info);
                break;
            case OverviewAction.ManualMode:
                _stateStore.SetBrightnessControlMode(BrightnessControlMode.Manual);
                _stateStore.AddEvent(
                    $"Manual brightness mode enabled at {_stateStore.ManualBrightnessPercent}%.",
                    RuntimeEventSeverity.Info);
                break;
            case OverviewAction.ManualDecreaseFast:
                _stateStore.AdjustManualBrightnessPercent(-10);
                break;
            case OverviewAction.ManualDecrease:
                _stateStore.AdjustManualBrightnessPercent(-1);
                break;
            case OverviewAction.ManualIncrease:
                _stateStore.AdjustManualBrightnessPercent(1);
                break;
            case OverviewAction.ManualIncreaseFast:
                _stateStore.AdjustManualBrightnessPercent(10);
                break;
        }
    }

    public void ActivateCalibrationAction(CalibrationAction action)
    {
        switch (action)
        {
            case CalibrationAction.UseCurrentBrightness:
                _stateStore.SelectCalibrationCurrentBrightness();
                break;
            case CalibrationAction.SetManualTarget:
                _stateStore.SelectCalibrationManualTarget();
                break;
            case CalibrationAction.Confirm:
                ConfirmCalibrationAction();
                break;
            case CalibrationAction.Cancel:
                _stateStore.CancelCalibrationWizard();
                _stateStore.SetCalibrationStatus("Calibration canceled.");
                _stateStore.AddEvent("Calibration wizard canceled.", RuntimeEventSeverity.Warning);
                break;
        }
    }

    public void HandleBack()
    {
        if (_stateStore.GetActiveScreen() == RuntimeScreen.Calibration)
        {
            _stateStore.BackCalibrationWizard();
            return;
        }

        _stateStore.SwitchScreen(RuntimeScreen.Overview);
    }

    public void HandleMouseClick(UiMouseClick click)
    {
        if (click.Y <= 3)
        {
            _stateStore.SwitchScreen(click.X switch
            {
                < 34 => RuntimeScreen.Overview,
                < 56 => RuntimeScreen.Calibration,
                < 74 => RuntimeScreen.Events,
                _ => RuntimeScreen.Diagnostics
            });
            return;
        }

        switch (_stateStore.GetActiveScreen())
        {
            case RuntimeScreen.Overview:
                ActivateOverviewAction(click.X switch
                {
                    < 24 => OverviewAction.AutoMode,
                    < 48 => OverviewAction.ManualMode,
                    < 66 => OverviewAction.ManualIncreaseFast,
                    _ => OverviewAction.ManualMode
                });
                break;
            case RuntimeScreen.Calibration:
                ActivateMouseCalibrationAction(click);
                break;
        }
    }

    private void ConfirmCalibrationAction()
    {
        var step = _stateStore.GetCalibrationWizardStep();
        if (step == CalibrationWizardStep.ManualTarget)
        {
            _stateStore.TryReviewManualCalibrationTarget();
            return;
        }

        if (step != CalibrationWizardStep.Review ||
            !_stateStore.TryGetReviewedCalibrationTarget(out var targetBrightnessPercent))
        {
            return;
        }

        if (_stateStore.TryRequestRecalibration(targetBrightnessPercent))
        {
            _stateStore.MarkCalibrationQueued();
            _stateStore.AddEvent(
                targetBrightnessPercent.HasValue
                    ? $"Recalibration requested with target brightness {targetBrightnessPercent}%."
                    : "Recalibration requested using current monitor brightness.",
                RuntimeEventSeverity.Warning);
        }
        else
        {
            _stateStore.AddEvent("Recalibration already pending.", RuntimeEventSeverity.Warning);
        }
    }

    private void ActivateMouseCalibrationAction(UiMouseClick click)
    {
        var step = _stateStore.GetCalibrationWizardStep();
        if (step == CalibrationWizardStep.ChooseTarget)
        {
            ActivateCalibrationAction(click.X switch
            {
                < 38 => CalibrationAction.UseCurrentBrightness,
                < 66 => CalibrationAction.SetManualTarget,
                _ => CalibrationAction.Cancel
            });
            return;
        }

        ActivateCalibrationAction(click.X < 40 ? CalibrationAction.Confirm : CalibrationAction.Cancel);
    }
}
