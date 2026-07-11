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
                break;
            case UiInputIntentKind.AppendDigit:
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

    public void HandleBack()
    {
        _stateStore.SwitchScreen(RuntimeScreen.Overview);
    }

    public void HandleMouseClick(UiMouseClick click)
    {
        if (click.Y <= 3)
        {
            _stateStore.SwitchScreen(click.X switch
            {
                < 18 => RuntimeScreen.Overview,
                < 36 => RuntimeScreen.Calibration,
                < 54 => RuntimeScreen.Events,
                < 72 => RuntimeScreen.Diagnostics,
                _ => RuntimeScreen.Update
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
        }
    }
}
