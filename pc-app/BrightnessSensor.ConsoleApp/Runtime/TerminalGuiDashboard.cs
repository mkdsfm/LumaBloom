using Terminal.Gui.Configuration;
using Terminal.Gui.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using System.Globalization;
using GuiAttribute = Terminal.Gui.Drawing.Attribute;

namespace BrightnessSensor.ConsoleApp.Runtime;

internal sealed class TerminalGuiDashboard
{
    private readonly RuntimeStateStore _stateStore;
    private readonly RuntimeInteractionController _controller;
    private readonly Action _requestUiStop;

    private readonly Label _title = new();
    private readonly Label _version = new();
    private readonly Label _clock = new();
    private readonly Label _status = new();
    private readonly Label _footer = new();
    private readonly Label _overviewText = new();
    private readonly Label _ambientText = new();
    private readonly Label _brightnessText = new();
    private readonly Label _calibrationText = new();
    private readonly Label _curveLightRow = new();
    private readonly Label _curveBrightnessRow = new();
    private readonly Label _generalText = new();
    private readonly Label _autostartText = new();
    private readonly Label _responseText = new();
    private readonly Label _eventsText = new();
    private readonly Label _diagnosticsText = new();
    private readonly View _contentView = new();
    private readonly View _settingsContentView = new();
    private readonly FrameView _settingsSidebar = new();
    private readonly FrameView _screenFrame = new();
    private readonly FrameView _sensorCard = new();
    private readonly FrameView _ambientCard = new();
    private readonly FrameView _brightnessCard = new();
    private readonly FrameView _curveTable = new();
    private readonly Button[] _screenButtons;
    private readonly Button _autoButton;
    private readonly Button _manualButton;
    private readonly Button _manualMinusFastButton;
    private readonly Button _manualMinusButton;
    private readonly Button _manualPlusButton;
    private readonly Button _manualPlusFastButton;
    private readonly Button _calibrationCurrentButton;
    private readonly Button _calibrationManualButton;
    private readonly Button _calibrationConfirmButton;
    private readonly Button _calibrationCancelButton;
    private readonly Button[] _curvePointButtons;
    private readonly Button _settingsCalibrationButton;
    private readonly Button _settingsGeneralButton;
    private readonly Button _settingsResponseButton;
    private readonly Button _languageEnglishButton;
    private readonly Button _languageRussianButton;
    private readonly Button _languageSpanishButton;
    private readonly Button _autostartButton;
    private readonly Button _processingAdcMinButton;
    private readonly Button _processingAdcMaxButton;
    private readonly Button _processingInvertButton;
    private readonly Button _processingEmaAlphaButton;
    private readonly Button _processingHysteresisButton;
    private readonly Button _processingStepButton;
    private readonly Button _processingGammaButton;
    private readonly FrameView _modalFrame = new();
    private readonly Label _modalDescription = new();
    private readonly TextField _modalInput = new();
    private readonly Label _modalError = new();
    private readonly CheckBox _modalTrueRadio = new();
    private readonly CheckBox _modalFalseRadio = new();
    private readonly Button _modalConfirmButton;
    private readonly Button _modalTestButton;
    private readonly Button _modalCancelButton;
    private ProcessingParameter? _activeProcessingModal;
    private int? _activeCurveLightPercent;
    private bool _activeCalibrationTargetModal;
    private bool _isUpdatingInvertRadio;

    private static readonly Scheme BaseScheme = CreateScheme(Color.Gray, Color.Black, Color.Black, Color.Green);
    private static readonly Scheme TitleScheme = CreateScheme(Color.BrightGreen, Color.Black, Color.Black, Color.BrightGreen);
    private static readonly Scheme AccentScheme = CreateScheme(Color.Green, Color.Black, Color.Black, Color.BrightGreen);
    private static readonly Scheme MutedScheme = CreateScheme(Color.DarkGray, Color.Black, Color.Black, Color.Green);
    private static readonly Scheme CardScheme = CreateScheme(Color.Gray, Color.Black, Color.Gray, Color.Black, Color.Gray);
    private static readonly Scheme ActiveButtonScheme = CreateScheme(Color.Black, Color.BrightGreen, Color.Black, Color.BrightGreen);
    private static readonly Scheme ButtonScheme = CreateScheme(Color.Green, Color.Black, Color.Black, Color.Green);
    private static readonly Scheme TabScheme = CreateScheme(Color.Green, Color.Black, Color.Green, Color.Black, Color.Green);
    private static readonly Scheme ActiveTabScheme = CreateScheme(Color.Black, Color.BrightGreen, Color.Black, Color.BrightGreen);
    private static readonly Scheme ModalScheme = CreateScheme(Color.Gray, Color.Black, Color.BrightGreen, Color.Black, Color.Gray);
    private const string BaseSchemeName = "lumabloom.base";
    private const string TitleSchemeName = "lumabloom.title";
    private const string AccentSchemeName = "lumabloom.accent";
    private const string MutedSchemeName = "lumabloom.muted";
    private const string CardSchemeName = "lumabloom.card";
    private const string ButtonSchemeName = "lumabloom.button";
    private const string ActiveButtonSchemeName = "lumabloom.button.active";
    private const string TabSchemeName = "lumabloom.tab";
    private const string ActiveTabSchemeName = "lumabloom.tab.active";
    private const string ModalSchemeName = "lumabloom.modal";

    public TerminalGuiDashboard(
        RuntimeStateStore stateStore,
        RuntimeInteractionController controller,
        Action requestUiStop)
    {
        _stateStore = stateStore;
        _controller = controller;
        _requestUiStop = requestUiStop;

        _screenButtons =
        [
            CreateButton("Overview", () => _stateStore.SwitchScreen(RuntimeScreen.Overview)),
            CreateButton("Settings", () => _stateStore.SwitchScreen(RuntimeScreen.Calibration)),
            CreateButton("Events", () => _stateStore.SwitchScreen(RuntimeScreen.Events)),
            CreateButton("Diagnostics", () => _stateStore.SwitchScreen(RuntimeScreen.Diagnostics))
        ];

        _autoButton = CreateButton("Auto", () => _controller.ActivateOverviewAction(OverviewAction.AutoMode));
        _manualButton = CreateButton("Manual", () => _controller.ActivateOverviewAction(OverviewAction.ManualMode));
        _manualMinusFastButton = CreateButton("-10", () => _controller.ActivateOverviewAction(OverviewAction.ManualDecreaseFast));
        _manualMinusButton = CreateButton("-1", () => _controller.ActivateOverviewAction(OverviewAction.ManualDecrease));
        _manualPlusButton = CreateButton("+1", () => _controller.ActivateOverviewAction(OverviewAction.ManualIncrease));
        _manualPlusFastButton = CreateButton("+10", () => _controller.ActivateOverviewAction(OverviewAction.ManualIncreaseFast));
        _calibrationCurrentButton = CreateButton("Use current", () => _controller.ActivateCalibrationAction(CalibrationAction.UseCurrentBrightness));
        _calibrationManualButton = CreateButton("Manual target", ShowCalibrationTargetModal);
        _calibrationConfirmButton = CreateButton("Confirm", () => _controller.ActivateCalibrationAction(CalibrationAction.Confirm));
        _calibrationCancelButton = CreateButton("Cancel", () => _controller.ActivateCalibrationAction(CalibrationAction.Cancel));
        _curvePointButtons =
        [
            CreateButton("0%", () => ShowCurvePointModal(0)),
            CreateButton("25%", () => ShowCurvePointModal(25)),
            CreateButton("50%", () => ShowCurvePointModal(50)),
            CreateButton("75%", () => ShowCurvePointModal(75)),
            CreateButton("100%", () => ShowCurvePointModal(100))
        ];
        _settingsCalibrationButton = CreateButton("Calibration", () => _stateStore.SetActiveSettingsSection(SettingsSection.Calibration));
        _settingsGeneralButton = CreateButton("General", () => _stateStore.SetActiveSettingsSection(SettingsSection.General));
        _settingsResponseButton = CreateButton("Реагирование", () => _stateStore.SetActiveSettingsSection(SettingsSection.Response));
        _languageEnglishButton = CreateButton("English", () => RequestLanguage(UiLanguage.English, "en"));
        _languageRussianButton = CreateButton("Русский", () => RequestLanguage(UiLanguage.Russian, "ru"));
        _languageSpanishButton = CreateButton("Español", () => RequestLanguage(UiLanguage.Spanish, "es"));
        _autostartButton = CreateButton("Autostart", ToggleAutostart);
        _processingAdcMinButton = CreateButton("adcMin", () => ShowProcessingModal(ProcessingParameter.AdcMin));
        _processingAdcMaxButton = CreateButton("adcMax", () => ShowProcessingModal(ProcessingParameter.AdcMax));
        _processingInvertButton = CreateButton("invert", () => ShowProcessingModal(ProcessingParameter.Invert));
        _processingEmaAlphaButton = CreateButton("emaAlpha", () => ShowProcessingModal(ProcessingParameter.EmaAlpha));
        _processingHysteresisButton = CreateButton("hysteresisPercent", () => ShowProcessingModal(ProcessingParameter.HysteresisPercent));
        _processingStepButton = CreateButton("maxBrightnessStepPercent", () => ShowProcessingModal(ProcessingParameter.MaxBrightnessStepPercent));
        _processingGammaButton = CreateButton("gamma", () => ShowProcessingModal(ProcessingParameter.Gamma));
        _modalConfirmButton = CreateButton("Confirm", ConfirmModal);
        _modalTestButton = CreateButton("Test", TestModalBrightness);
        _modalCancelButton = CreateButton("Cancel", CloseModal);
    }

    public Window Build()
    {
        RegisterSchemes();

        var window = new Window
        {
            Title = "LumaBloom",
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            SchemeName = BaseSchemeName,
            BorderStyle = LineStyle.None
        };
        window.Title = string.Empty;

        _title.Text = "LumaBloom";
        _title.X = 1;
        _title.Y = 0;
        _title.SchemeName = TitleSchemeName;
        _version.Text = "v1.2.0";
        _version.X = Pos.Right(_title) + 1;
        _version.Y = 0;
        _version.SchemeName = AccentSchemeName;
        _clock.X = Pos.AnchorEnd(10);
        _clock.Y = 0;
        _clock.SchemeName = MutedSchemeName;
        _status.X = Pos.Center();
        _status.Y = 0;
        _status.Width = Dim.Percent(42);
        _status.SchemeName = AccentSchemeName;
        window.Add(_title);
        window.Add(_version);
        window.Add(_clock);
        window.Add(_status);

        for (var i = 0; i < _screenButtons.Length; i++)
        {
            _screenButtons[i].X = 1 + (i * 18);
            _screenButtons[i].Y = 2;
            _screenButtons[i].Width = 16;
            _screenButtons[i].CanFocus = false;
            _screenButtons[i].MouseHighlightStates = MouseState.None;
            _screenButtons[i].SchemeName = TabSchemeName;
            window.Add(_screenButtons[i]);
        }

        _contentView.X = 0;
        _contentView.Y = 4;
        _contentView.Width = Dim.Fill();
        _contentView.Height = Dim.Fill(3);
        _contentView.SchemeName = BaseSchemeName;

        _screenFrame.X = 0;
        _screenFrame.Y = 0;
        _screenFrame.Width = Dim.Fill();
        _screenFrame.Height = Dim.Fill();
        _screenFrame.SchemeName = CardSchemeName;

        _sensorCard.Title = "Sensor";
        _sensorCard.X = 1;
        _sensorCard.Y = 0;
        _sensorCard.Width = Dim.Percent(33);
        _sensorCard.Height = Dim.Fill(5);
        _sensorCard.SchemeName = CardSchemeName;
        _overviewText.X = 1;
        _overviewText.Y = 1;
        _overviewText.Width = Dim.Fill(2);
        _overviewText.Height = Dim.Fill(1);
        _overviewText.SchemeName = BaseSchemeName;
        _sensorCard.Add(_overviewText);

        _ambientCard.Title = "Ambient Light";
        _ambientCard.X = Pos.Right(_sensorCard) + 1;
        _ambientCard.Y = 0;
        _ambientCard.Width = Dim.Percent(33);
        _ambientCard.Height = Dim.Fill(5);
        _ambientCard.SchemeName = CardSchemeName;
        _ambientText.X = 1;
        _ambientText.Y = 1;
        _ambientText.Width = Dim.Fill(2);
        _ambientText.Height = Dim.Fill(1);
        _ambientText.SchemeName = BaseSchemeName;
        _ambientCard.Add(_ambientText);

        _brightnessCard.Title = "Brightness Control";
        _brightnessCard.X = Pos.Right(_ambientCard) + 1;
        _brightnessCard.Y = 0;
        _brightnessCard.Width = Dim.Fill(1);
        _brightnessCard.Height = Dim.Fill(5);
        _brightnessCard.SchemeName = CardSchemeName;
        _brightnessText.X = 1;
        _brightnessText.Y = 1;
        _brightnessText.Width = Dim.Fill(2);
        _brightnessText.Height = Dim.Fill(6);
        _brightnessText.SchemeName = BaseSchemeName;
        _brightnessCard.Add(_brightnessText);

        _autoButton.X = 1;
        _autoButton.Y = Pos.AnchorEnd(5);
        _manualButton.X = Pos.Right(_autoButton) + 1;
        _manualButton.Y = Pos.AnchorEnd(5);
        _manualMinusFastButton.X = 1;
        _manualMinusFastButton.Y = Pos.AnchorEnd(3);
        _manualMinusButton.X = Pos.Right(_manualMinusFastButton) + 1;
        _manualMinusButton.Y = Pos.AnchorEnd(3);
        _manualPlusButton.X = Pos.Right(_manualMinusButton) + 1;
        _manualPlusButton.Y = Pos.AnchorEnd(3);
        _manualPlusFastButton.X = Pos.Right(_manualPlusButton) + 1;
        _manualPlusFastButton.Y = Pos.AnchorEnd(3);
        foreach (var button in new[]
        {
            _autoButton,
            _manualButton,
            _manualMinusFastButton,
            _manualMinusButton,
            _manualPlusButton,
            _manualPlusFastButton,
            _calibrationCurrentButton,
            _calibrationManualButton,
            _calibrationConfirmButton,
            _calibrationCancelButton,
            _curvePointButtons[0],
            _curvePointButtons[1],
            _curvePointButtons[2],
            _curvePointButtons[3],
            _curvePointButtons[4],
            _settingsCalibrationButton,
            _settingsGeneralButton,
            _settingsResponseButton,
            _languageEnglishButton,
            _languageRussianButton,
            _languageSpanishButton,
            _autostartButton,
            _processingAdcMinButton,
            _processingAdcMaxButton,
            _processingInvertButton,
            _processingEmaAlphaButton,
            _processingHysteresisButton,
            _processingStepButton,
            _processingGammaButton,
            _modalConfirmButton,
            _modalTestButton,
            _modalCancelButton
        })
        {
            button.SchemeName = ButtonSchemeName;
        }

        _brightnessCard.Add(_autoButton);
        _brightnessCard.Add(_manualButton);
        _brightnessCard.Add(_manualMinusFastButton);
        _brightnessCard.Add(_manualMinusButton);
        _brightnessCard.Add(_manualPlusButton);
        _brightnessCard.Add(_manualPlusFastButton);

        _calibrationText.X = 1;
        _calibrationText.Y = 1;
        _calibrationText.Width = Dim.Fill(2);
        _calibrationText.Height = 8;
        _calibrationText.SchemeName = BaseSchemeName;

        _curveTable.X = 1;
        _curveTable.Y = 10;
        _curveTable.Width = 66;
        _curveTable.Height = 6;
        _curveTable.SchemeName = CardSchemeName;
        _curveLightRow.X = 1;
        _curveLightRow.Y = 1;
        _curveLightRow.Width = Dim.Fill(2);
        _curveLightRow.Height = 1;
        _curveLightRow.SchemeName = BaseSchemeName;
        _curveBrightnessRow.X = 1;
        _curveBrightnessRow.Y = 3;
        _curveBrightnessRow.Width = 18;
        _curveBrightnessRow.Height = 1;
        _curveBrightnessRow.SchemeName = BaseSchemeName;
        _calibrationCurrentButton.X = 1;
        _calibrationCurrentButton.Y = Pos.AnchorEnd(3);
        _calibrationManualButton.X = Pos.Right(_calibrationCurrentButton) + 1;
        _calibrationManualButton.Y = Pos.AnchorEnd(3);
        _calibrationConfirmButton.X = Pos.Right(_calibrationManualButton) + 1;
        _calibrationConfirmButton.Y = Pos.AnchorEnd(3);
        _calibrationCancelButton.X = Pos.AnchorEnd(12);
        _calibrationCancelButton.Y = Pos.AnchorEnd(3);
        for (var i = 0; i < _curvePointButtons.Length; i++)
        {
            _curvePointButtons[i].X = 20 + (i * 9);
            _curvePointButtons[i].Y = 3;
            _curvePointButtons[i].Width = 8;
        }
        _curveTable.Add(_curveLightRow);
        _curveTable.Add(_curveBrightnessRow);
        foreach (var button in _curvePointButtons)
        {
            _curveTable.Add(button);
        }

        _settingsContentView.X = 1;
        _settingsContentView.Y = 1;
        _settingsContentView.Width = Dim.Fill(24);
        _settingsContentView.Height = Dim.Fill(2);
        _settingsContentView.SchemeName = BaseSchemeName;

        _settingsSidebar.Title = "Settings";
        _settingsSidebar.X = Pos.AnchorEnd(22);
        _settingsSidebar.Y = 1;
        _settingsSidebar.Width = 22;
        _settingsSidebar.Height = Dim.Fill(1);
        _settingsSidebar.SchemeName = CardSchemeName;
        _settingsGeneralButton.X = 1;
        _settingsGeneralButton.Y = 2;
        _settingsGeneralButton.Width = Dim.Fill(2);
        _settingsCalibrationButton.X = 1;
        _settingsCalibrationButton.Y = 4;
        _settingsCalibrationButton.Width = Dim.Fill(2);
        _settingsResponseButton.X = 1;
        _settingsResponseButton.Y = 6;
        _settingsResponseButton.Width = Dim.Fill(2);
        _settingsSidebar.Add(_settingsGeneralButton);
        _settingsSidebar.Add(_settingsCalibrationButton);
        _settingsSidebar.Add(_settingsResponseButton);

        _generalText.X = 1;
        _generalText.Y = 1;
        _generalText.Width = Dim.Fill(2);
        _generalText.Height = 5;
        _generalText.SchemeName = BaseSchemeName;
        _languageEnglishButton.X = 1;
        _languageEnglishButton.Y = 6;
        _languageEnglishButton.Width = 14;
        _languageRussianButton.X = 17;
        _languageRussianButton.Y = 6;
        _languageRussianButton.Width = 14;
        _languageSpanishButton.X = 33;
        _languageSpanishButton.Y = 6;
        _languageSpanishButton.Width = 14;
        _autostartText.X = 1;
        _autostartText.Y = 9;
        _autostartText.Width = Dim.Fill(2);
        _autostartText.Height = 1;
        _autostartText.SchemeName = BaseSchemeName;
        _autostartButton.X = 1;
        _autostartButton.Y = 11;
        _autostartButton.Width = 28;

        _responseText.X = 1;
        _responseText.Y = 1;
        _responseText.Width = Dim.Fill(2);
        _responseText.Height = 11;
        _responseText.SchemeName = BaseSchemeName;
        var processingButtons = new[]
        {
            _processingAdcMinButton,
            _processingAdcMaxButton,
            _processingInvertButton,
            _processingEmaAlphaButton,
            _processingHysteresisButton,
            _processingStepButton,
            _processingGammaButton
        };
        for (var i = 0; i < processingButtons.Length; i++)
        {
            processingButtons[i].X = 1 + ((i % 2) * 30);
            processingButtons[i].Y = 12 + (i / 2 * 2);
            processingButtons[i].Width = 28;
        }

        _modalFrame.Visible = false;
        _modalFrame.X = Pos.Center();
        _modalFrame.Y = Pos.Center();
        _modalFrame.Width = 72;
        _modalFrame.Height = 12;
        _modalFrame.SchemeName = ModalSchemeName;
        _modalDescription.X = 1;
        _modalDescription.Y = 1;
        _modalDescription.Width = Dim.Fill(2);
        _modalDescription.Height = 2;
        _modalInput.X = 1;
        _modalInput.Y = 4;
        _modalInput.Width = Dim.Fill(2);
        _modalTrueRadio.X = 1;
        _modalTrueRadio.Y = 4;
        _modalTrueRadio.Width = 12;
        _modalTrueRadio.Text = "true";
        _modalTrueRadio.RadioStyle = true;
        _modalTrueRadio.SchemeName = ButtonSchemeName;
        _modalTrueRadio.Visible = false;
        _modalTrueRadio.ValueChanged += (_, _) => SelectInvertRadio(true);
        _modalFalseRadio.X = Pos.Right(_modalTrueRadio) + 3;
        _modalFalseRadio.Y = 4;
        _modalFalseRadio.Width = 12;
        _modalFalseRadio.Text = "false";
        _modalFalseRadio.RadioStyle = true;
        _modalFalseRadio.SchemeName = ButtonSchemeName;
        _modalFalseRadio.Visible = false;
        _modalFalseRadio.ValueChanged += (_, _) => SelectInvertRadio(false);
        _modalError.X = 1;
        _modalError.Y = 6;
        _modalError.Width = Dim.Fill(2);
        _modalError.Height = 1;
        _modalError.SchemeName = MutedSchemeName;
        _modalConfirmButton.X = Pos.AnchorEnd(50);
        _modalConfirmButton.Y = Pos.AnchorEnd(2);
        _modalConfirmButton.Width = 16;
        _modalTestButton.X = Pos.AnchorEnd(32);
        _modalTestButton.Y = Pos.AnchorEnd(2);
        _modalTestButton.Width = 14;
        _modalCancelButton.X = Pos.AnchorEnd(16);
        _modalCancelButton.Y = Pos.AnchorEnd(2);
        _modalCancelButton.Width = 14;
        _modalFrame.Add(_modalDescription);
        _modalFrame.Add(_modalInput);
        _modalFrame.Add(_modalTrueRadio);
        _modalFrame.Add(_modalFalseRadio);
        _modalFrame.Add(_modalError);
        _modalFrame.Add(_modalConfirmButton);
        _modalFrame.Add(_modalTestButton);
        _modalFrame.Add(_modalCancelButton);

        _eventsText.X = 1;
        _eventsText.Y = 1;
        _eventsText.Width = Dim.Fill(2);
        _eventsText.Height = Dim.Fill(1);
        _eventsText.SchemeName = BaseSchemeName;

        _diagnosticsText.X = 1;
        _diagnosticsText.Y = 1;
        _diagnosticsText.Width = Dim.Fill(2);
        _diagnosticsText.Height = Dim.Fill(1);
        _diagnosticsText.SchemeName = BaseSchemeName;

        _footer.Text = "Left/Right: tabs   Up/Down: focus   Enter: activate   Esc: back   Mouse: select";
        _footer.X = 1;
        _footer.Y = Pos.AnchorEnd(1);
        _footer.Width = Dim.Fill(2);
        _footer.Height = 1;
        _footer.SchemeName = MutedSchemeName;

        window.Add(_contentView);
        window.Add(_footer);
        window.Add(_modalFrame);

        return window;
    }

    public void Refresh()
    {
        var snapshot = _stateStore.GetSnapshot();
        var localizer = new Localizer(snapshot.Language);
        var percent = GetNormalizedSensorPercent(snapshot);
        var ambient = percent.HasValue ? $"{percent.Value}%" : "--%";
        var connected = snapshot.LatestSensor is not null ? "CONNECTED" : "WAITING";
        var mode = snapshot.BrightnessControlMode == BrightnessControlMode.Auto ? "AUTO" : "MANUAL";

        ApplyLocalizedChrome(localizer);
        _clock.Text = DateTime.Now.ToString("HH:mm:ss");
        _status.Text = $"{connected}   {ambient}   {mode}";

        _autoButton.SchemeName = snapshot.BrightnessControlMode == BrightnessControlMode.Auto ? ActiveButtonSchemeName : ButtonSchemeName;
        _manualButton.SchemeName = snapshot.BrightnessControlMode == BrightnessControlMode.Manual ? ActiveButtonSchemeName : ButtonSchemeName;

        _overviewText.Text =
            $"{localizer["overview.state"]}: {connected}{Environment.NewLine}{Environment.NewLine}" +
            $"{localizer["overview.lastRead"]}: {snapshot.LatestSensor?.ReceivedAt.LocalDateTime.ToString("HH:mm:ss") ?? "--:--:--"}";
        _ambientText.Text =
            $"{localizer["overview.normalized"]}: {ambient}{Environment.NewLine}{Environment.NewLine}" +
            $"{localizer["overview.level"]}: {GetSunState(percent)}";
        var lastAppliedMonitor = snapshot.Monitors.FirstOrDefault(monitor => monitor.LastAppliedBrightness.HasValue);
        var applied = snapshot.BrightnessControlMode == BrightnessControlMode.Manual && snapshot.LastManualAppliedBrightnessPercent.HasValue
            ? $"{snapshot.LastManualAppliedBrightnessPercent}%"
            : lastAppliedMonitor?.LastAppliedBrightness.HasValue == true
                ? $"{lastAppliedMonitor.LastAppliedBrightness}%"
                : "n/a";

        _brightnessText.Text =
            $"{localizer["status.mode"]}: {mode}{Environment.NewLine}" +
            $"{localizer["overview.manualTarget"]}: {snapshot.ManualBrightnessPercent}%{Environment.NewLine}" +
            $"{localizer["overview.lastApplied"]}: {applied ?? localizer["value.none"]}{Environment.NewLine}" +
            $"{localizer["status.monitors"]}: {snapshot.Monitors.Count(monitor => monitor.IsEnabled)}/{snapshot.Monitors.Count} {localizer["overview.active"]}";
        _calibrationText.Text = BuildCalibrationText(snapshot, localizer);
        _generalText.Text = BuildGeneralText(snapshot, localizer);
        _autostartText.Text = BuildAutostartText(snapshot, localizer);
        _responseText.Text = BuildResponseText(snapshot, localizer);
        _eventsText.Text = BuildEventsText(snapshot, localizer);
        _diagnosticsText.Text = BuildDiagnosticsText(snapshot);
        _curveLightRow.Text = BuildCurveLightRow(localizer);
        _curveBrightnessRow.Text = localizer["settings.curve.display"];

        UpdateCurveButtons(snapshot);
        UpdateSettingsSchemes(snapshot);

        RenderActiveScreen(snapshot.ActiveScreen);
    }

    private void ApplyLocalizedChrome(Localizer localizer)
    {
        _screenButtons[(int)RuntimeScreen.Overview].Text = localizer["screen.overview"];
        _screenButtons[(int)RuntimeScreen.Calibration].Text = localizer["screen.calibration"];
        _screenButtons[(int)RuntimeScreen.Events].Text = localizer["screen.events"];
        _screenButtons[(int)RuntimeScreen.Diagnostics].Text = localizer["screen.diagnostics"];
        _sensorCard.Title = localizer["status.sensor"];
        _ambientCard.Title = localizer["overview.ambientLight"];
        _brightnessCard.Title = localizer["overview.brightnessControl"];
        _settingsSidebar.Title = localizer["screen.calibration"];
        _settingsCalibrationButton.Text = localizer["settings.calibration"];
        _settingsGeneralButton.Text = localizer["settings.general"];
        _settingsResponseButton.Text = localizer["settings.response"];
        _autostartButton.Text = _stateStore.GetSnapshot().AutostartEnabled
            ? localizer["settings.autostart.disable"]
            : localizer["settings.autostart.enable"];
        _autoButton.Text = localizer["mode.auto"];
        _manualButton.Text = localizer["mode.manual"];
        _calibrationCurrentButton.Text = localizer["action.useCurrent"];
        _calibrationManualButton.Text = localizer["action.manualTarget"];
        _calibrationConfirmButton.Text = localizer["action.confirm"];
        _calibrationCancelButton.Text = localizer["action.cancel"];
        _modalConfirmButton.Text = localizer["action.confirm"];
        _modalTestButton.Text = localizer["action.test"];
        _modalCancelButton.Text = localizer["action.cancel"];
        _footer.Text = localizer["nav.hint"];
    }

    public void HandleBack()
    {
        if (_modalFrame.Visible)
        {
            CloseModal();
            Refresh();
            return;
        }

        _controller.HandleBack();
        Refresh();
    }

    public void HandleLeft()
    {
        _stateStore.MoveScreen(-1);
        Refresh();
    }

    public void HandleRight()
    {
        _stateStore.MoveScreen(1);
        Refresh();
    }

    public void HandleEnter()
    {
        if (_modalFrame.Visible)
        {
            ConfirmModal();
            return;
        }

        _controller.ActivateFocused();
        Refresh();
    }

    public void RequestStop()
    {
        _requestUiStop();
    }

    public bool IsModalOpen => _modalFrame.Visible;

    private void RenderActiveScreen(RuntimeScreen screen)
    {
        var localizer = new Localizer(_stateStore.GetSnapshot().Language);
        _contentView.RemoveAll();
        foreach (var button in _screenButtons)
        {
            button.Text = button.Text?.ToString() ?? string.Empty;
            button.SchemeName = TabSchemeName;
            button.SetNeedsDraw();
        }

        _screenButtons[(int)screen].SchemeName = ActiveTabSchemeName;
        _screenButtons[(int)screen].SetNeedsDraw();

        switch (screen)
        {
            case RuntimeScreen.Calibration:
                _screenFrame.Title = localizer["screen.calibration"];
                _screenFrame.RemoveAll();
                RenderSettingsSection(_stateStore.GetSnapshot());
                _screenFrame.Add(_settingsContentView);
                _screenFrame.Add(_settingsSidebar);
                _contentView.Add(_screenFrame);
                break;
            case RuntimeScreen.Events:
                _screenFrame.Title = localizer["screen.events"];
                _screenFrame.RemoveAll();
                _screenFrame.Add(_eventsText);
                _contentView.Add(_screenFrame);
                break;
            case RuntimeScreen.Diagnostics:
                _screenFrame.Title = localizer["screen.diagnostics"];
                _screenFrame.RemoveAll();
                _screenFrame.Add(_diagnosticsText);
                _contentView.Add(_screenFrame);
                break;
            default:
                LayoutOverviewCards(_stateStore.GetSnapshot().IsCompact);
                _contentView.Add(_sensorCard);
                _contentView.Add(_ambientCard);
                _contentView.Add(_brightnessCard);
                break;
        }
    }

    private void LayoutOverviewCards(bool isCompact)
    {
        _sensorCard.X = 1;
        _sensorCard.Y = 0;
        _sensorCard.Width = isCompact ? Dim.Fill(1) : Dim.Percent(33);
        _sensorCard.Height = isCompact ? 5 : Dim.Fill(5);

        _ambientCard.X = isCompact ? 1 : Pos.Right(_sensorCard) + 1;
        _ambientCard.Y = isCompact ? Pos.Bottom(_sensorCard) : 0;
        _ambientCard.Width = isCompact ? Dim.Fill(1) : Dim.Percent(33);
        _ambientCard.Height = isCompact ? 5 : Dim.Fill(5);

        _brightnessCard.X = isCompact ? 1 : Pos.Right(_ambientCard) + 1;
        _brightnessCard.Y = isCompact ? Pos.Bottom(_ambientCard) : 0;
        _brightnessCard.Width = Dim.Fill(1);
        _brightnessCard.Height = isCompact ? Dim.Fill(5) : Dim.Fill(5);
    }

    private void RenderSettingsSection(DashboardSnapshot snapshot)
    {
        _settingsContentView.RemoveAll();
        switch (snapshot.ActiveSettingsSection)
        {
            case SettingsSection.General:
                _settingsContentView.Add(_generalText);
                _settingsContentView.Add(_languageEnglishButton);
                _settingsContentView.Add(_languageRussianButton);
                _settingsContentView.Add(_languageSpanishButton);
                _settingsContentView.Add(_autostartText);
                _settingsContentView.Add(_autostartButton);
                break;
            case SettingsSection.Response:
                _settingsContentView.Add(_responseText);
                _settingsContentView.Add(_processingAdcMinButton);
                _settingsContentView.Add(_processingAdcMaxButton);
                _settingsContentView.Add(_processingInvertButton);
                _settingsContentView.Add(_processingEmaAlphaButton);
                _settingsContentView.Add(_processingHysteresisButton);
                _settingsContentView.Add(_processingStepButton);
                _settingsContentView.Add(_processingGammaButton);
                break;
            default:
                _settingsContentView.Add(_calibrationText);
                _settingsContentView.Add(_curveTable);
                break;
        }
    }

    private void UpdateSettingsSchemes(DashboardSnapshot snapshot)
    {
        _settingsCalibrationButton.SchemeName = snapshot.ActiveSettingsSection == SettingsSection.Calibration
            ? ActiveButtonSchemeName
            : ButtonSchemeName;
        _settingsGeneralButton.SchemeName = snapshot.ActiveSettingsSection == SettingsSection.General
            ? ActiveButtonSchemeName
            : ButtonSchemeName;
        _settingsResponseButton.SchemeName = snapshot.ActiveSettingsSection == SettingsSection.Response
            ? ActiveButtonSchemeName
            : ButtonSchemeName;

        _languageEnglishButton.SchemeName = snapshot.Language == UiLanguage.English ? ActiveButtonSchemeName : ButtonSchemeName;
        _languageRussianButton.SchemeName = snapshot.Language == UiLanguage.Russian ? ActiveButtonSchemeName : ButtonSchemeName;
        _languageSpanishButton.SchemeName = snapshot.Language == UiLanguage.Spanish ? ActiveButtonSchemeName : ButtonSchemeName;
        _autostartButton.SchemeName = snapshot.AutostartEnabled ? ActiveButtonSchemeName : ButtonSchemeName;
        _languageEnglishButton.SetNeedsDraw();
        _languageRussianButton.SetNeedsDraw();
        _languageSpanishButton.SetNeedsDraw();
        _autostartText.SetNeedsDraw();
        _autostartButton.SetNeedsDraw();
    }

    private void RequestLanguage(UiLanguage language, string code)
    {
        _stateStore.RequestLanguageChange(language, code);
        Refresh();
    }

    private void ToggleAutostart()
    {
        _stateStore.RequestAutostartChange(!_stateStore.GetSnapshot().AutostartEnabled);
        Refresh();
    }

    private void ShowCalibrationTargetModal()
    {
        var snapshot = _stateStore.GetSnapshot();
        var localizer = new Localizer(snapshot.Language);
        _activeCalibrationTargetModal = true;
        _activeProcessingModal = null;
        _modalFrame.Title = localizer["calibration.manualValue"];
        _modalDescription.Text = localizer["modal.manualTargetDescription"];
        _modalInput.Text = snapshot.CalibrationManualInputBuffer.Length > 0
            ? snapshot.CalibrationManualInputBuffer
            : "50";
        SetModalInputMode(true);
        _modalError.Text = string.Empty;
        _modalTestButton.Visible = true;
        _modalFrame.Visible = true;
        _modalInput.SetFocus();
    }

    private void ShowProcessingModal(ProcessingParameter parameter)
    {
        var snapshot = _stateStore.GetSnapshot();
        var localizer = new Localizer(snapshot.Language);
        _activeCalibrationTargetModal = false;
        _activeProcessingModal = parameter;
        _modalFrame.Title = GetProcessingLabel(parameter);
        _modalDescription.Text = GetProcessingModalDescription(parameter, localizer);
        if (parameter == ProcessingParameter.Invert)
        {
            SetModalInputMode(false);
            SelectInvertRadio(snapshot.ProcessingInvert ?? false);
        }
        else
        {
            SetModalInputMode(true);
            _modalInput.Text = GetProcessingValue(snapshot, parameter);
        }

        _modalError.Text = string.Empty;
        _modalTestButton.Visible = false;
        _modalFrame.Visible = true;
        if (parameter == ProcessingParameter.Invert)
        {
            (IsChecked(_modalTrueRadio) ? _modalTrueRadio : _modalFalseRadio).SetFocus();
        }
        else
        {
            _modalInput.SetFocus();
        }
    }

    private void ShowCurvePointModal(int lightPercent)
    {
        var snapshot = _stateStore.GetSnapshot();
        var localizer = new Localizer(snapshot.Language);
        _activeCalibrationTargetModal = false;
        _activeProcessingModal = null;
        _activeCurveLightPercent = lightPercent;
        _modalFrame.Title = $"{localizer["settings.curve.point"]} {lightPercent}%";
        _modalDescription.Text = localizer["settings.curve.modal"];
        _modalInput.Text = GetCurveBrightness(snapshot, lightPercent).ToString(CultureInfo.InvariantCulture);
        SetModalInputMode(true);
        _modalError.Text = string.Empty;
        _modalTestButton.Visible = true;
        _modalFrame.Visible = true;
        _modalInput.SetFocus();
    }

    private void TestModalBrightness()
    {
        var localizer = new Localizer(_stateStore.GetSnapshot().Language);
        var value = _modalInput.Text?.ToString() ?? string.Empty;
        if (!TryParseInt(value, out var brightness) || brightness is < 0 or > 100)
        {
            _modalError.Text = localizer["calibration.invalid"];
            return;
        }

        _stateStore.RequestTestBrightness(brightness);
        _modalError.Text = localizer["modal.testQueued"];
    }

    private void ConfirmModal()
    {
        var value = _modalInput.Text?.ToString() ?? string.Empty;
        if (_activeCalibrationTargetModal)
        {
            if (!TryParseInt(value, out var parsed) || parsed is < 0 or > 100)
            {
                _modalError.Text = new Localizer(_stateStore.GetSnapshot().Language)["calibration.invalid"];
                return;
            }

            _stateStore.SelectCalibrationManualTarget(parsed);
            CloseModal();
            Refresh();
            return;
        }

        if (!_activeProcessingModal.HasValue)
        {
            if (_activeCurveLightPercent.HasValue)
            {
                if (!TryParseInt(value, out var brightness) || brightness is < 0 or > 100)
                {
                    _modalError.Text = new Localizer(_stateStore.GetSnapshot().Language)["calibration.invalid"];
                    return;
                }

                _stateStore.RequestBrightnessCurveUpdate(_activeCurveLightPercent.Value, brightness);
                CloseModal();
                Refresh();
                return;
            }

            CloseModal();
            return;
        }

        var snapshot = _stateStore.GetSnapshot();
        if (_activeProcessingModal.Value == ProcessingParameter.Invert)
        {
            _stateStore.RequestProcessingUpdate(ProcessingParameter.Invert, IsChecked(_modalTrueRadio) ? "true" : "false");
            CloseModal();
            Refresh();
            return;
        }

        var validationError = ValidateProcessingInput(snapshot, _activeProcessingModal.Value, value, new Localizer(snapshot.Language));
        if (validationError is not null)
        {
            _modalError.Text = validationError;
            return;
        }

        _stateStore.RequestProcessingUpdate(_activeProcessingModal.Value, NormalizeProcessingInput(_activeProcessingModal.Value, value));
        CloseModal();
        Refresh();
    }

    private void CloseModal()
    {
        _modalFrame.Visible = false;
        _modalError.Text = string.Empty;
        _activeCalibrationTargetModal = false;
        _activeProcessingModal = null;
        _activeCurveLightPercent = null;
        SetModalInputMode(true);
        _modalTestButton.Visible = false;
    }

    private void SetModalInputMode(bool usesTextInput)
    {
        _modalInput.Visible = usesTextInput;
        _modalTrueRadio.Visible = !usesTextInput;
        _modalFalseRadio.Visible = !usesTextInput;
    }

    private void SelectInvertRadio(bool value)
    {
        if (_isUpdatingInvertRadio)
        {
            return;
        }

        _isUpdatingInvertRadio = true;
        _modalTrueRadio.Value = value ? CheckState.Checked : CheckState.UnChecked;
        _modalFalseRadio.Value = value ? CheckState.UnChecked : CheckState.Checked;
        _isUpdatingInvertRadio = false;
    }

    private static bool IsChecked(CheckBox checkBox)
    {
        return checkBox.Value == CheckState.Checked;
    }

    private void UpdateCurveButtons(DashboardSnapshot snapshot)
    {
        var lights = new[] { 0, 25, 50, 75, 100 };
        for (var i = 0; i < _curvePointButtons.Length; i++)
        {
            var light = lights[i];
            _curvePointButtons[i].Text = $"{GetCurveBrightness(snapshot, light)}%";
        }
    }

    private static string BuildCurveLightRow(Localizer localizer)
    {
        var cells = new[] { 0, 25, 50, 75, 100 }
            .Select(light => Center($"{light}%", 9));
        return $"{localizer["settings.curve.ambient"],-18} {string.Concat(cells)}";
    }

    private static string Center(string value, int width)
    {
        if (value.Length >= width)
        {
            return value;
        }

        var left = (width - value.Length) / 2;
        return new string(' ', left) + value + new string(' ', width - value.Length - left);
    }

    private static int GetCurveBrightness(DashboardSnapshot snapshot, int lightPercent)
    {
        var point = snapshot.BrightnessCurve?
            .FirstOrDefault(candidate => candidate.LightPercent == lightPercent);
        if (point is not null)
        {
            return point.BrightnessPercent;
        }

        var curve = snapshot.BrightnessCurve?.OrderBy(candidate => candidate.LightPercent).ToArray();
        if (curve is null || curve.Length == 0)
        {
            return lightPercent;
        }

        if (lightPercent <= curve[0].LightPercent)
        {
            return curve[0].BrightnessPercent;
        }

        if (lightPercent >= curve[^1].LightPercent)
        {
            return curve[^1].BrightnessPercent;
        }

        for (var i = 0; i < curve.Length - 1; i++)
        {
            var left = curve[i];
            var right = curve[i + 1];
            if (lightPercent < left.LightPercent || lightPercent > right.LightPercent)
            {
                continue;
            }

            var ratio = (lightPercent - left.LightPercent) / (double)(right.LightPercent - left.LightPercent);
            return (int)Math.Round(
                left.BrightnessPercent + (ratio * (right.BrightnessPercent - left.BrightnessPercent)),
                MidpointRounding.AwayFromZero);
        }

        return curve[^1].BrightnessPercent;
    }

    private static string? ValidateProcessingInput(
        DashboardSnapshot snapshot,
        ProcessingParameter parameter,
        string value,
        Localizer localizer)
    {
        if (parameter == ProcessingParameter.Invert)
        {
            return bool.TryParse(value, out _)
                ? null
                : localizer["validation.trueFalse"];
        }

        if (parameter is ProcessingParameter.EmaAlpha or ProcessingParameter.Gamma)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return localizer["validation.decimal"];
            }

            if (parameter == ProcessingParameter.EmaAlpha && parsed is <= 0 or > 1)
            {
                return localizer["validation.emaAlpha"];
            }

            if (parameter == ProcessingParameter.Gamma && parsed <= 0)
            {
                return localizer["validation.gamma"];
            }

            return null;
        }

        if (!TryParseInt(value, out var intValue))
        {
            return localizer["validation.integer"];
        }

        return parameter switch
        {
            ProcessingParameter.AdcMin when snapshot.ProcessingAdcMax.HasValue && intValue >= snapshot.ProcessingAdcMax.Value =>
                localizer["validation.adcMin"],
            ProcessingParameter.AdcMax when snapshot.ProcessingAdcMin.HasValue && intValue <= snapshot.ProcessingAdcMin.Value =>
                localizer["validation.adcMax"],
            ProcessingParameter.HysteresisPercent when intValue is < 0 or > 100 =>
                localizer["validation.hysteresis"],
            ProcessingParameter.MaxBrightnessStepPercent when intValue is <= 0 or > 100 =>
                localizer["validation.step"],
            _ => null
        };
    }

    private static string NormalizeProcessingInput(ProcessingParameter parameter, string value)
    {
        if (parameter == ProcessingParameter.Invert)
        {
            return bool.Parse(value).ToString();
        }

        if (parameter is ProcessingParameter.EmaAlpha or ProcessingParameter.Gamma)
        {
            return double.Parse(value, CultureInfo.InvariantCulture).ToString("0.0###############", CultureInfo.InvariantCulture);
        }

        return int.Parse(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseInt(string value, out int parsed)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
    }

    private static string GetProcessingValue(DashboardSnapshot snapshot, ProcessingParameter parameter)
    {
        return parameter switch
        {
            ProcessingParameter.AdcMin => snapshot.ProcessingAdcMin?.ToString(CultureInfo.InvariantCulture) ?? "0",
            ProcessingParameter.AdcMax => snapshot.ProcessingAdcMax?.ToString(CultureInfo.InvariantCulture) ?? "1000",
            ProcessingParameter.Invert => snapshot.ProcessingInvert?.ToString() ?? "False",
            ProcessingParameter.EmaAlpha => FormatNullableNumber(snapshot.ProcessingEmaAlpha) ?? "0.2",
            ProcessingParameter.HysteresisPercent => snapshot.ProcessingHysteresisPercent?.ToString(CultureInfo.InvariantCulture) ?? "1",
            ProcessingParameter.MaxBrightnessStepPercent => snapshot.ProcessingMaxBrightnessStepPercent?.ToString(CultureInfo.InvariantCulture) ?? "2",
            ProcessingParameter.Gamma => FormatNullableNumber(snapshot.ProcessingGamma) ?? "1.0",
            _ => string.Empty
        };
    }

    private static string GetProcessingLabel(ProcessingParameter parameter)
    {
        return parameter switch
        {
            ProcessingParameter.AdcMin => "adcMin",
            ProcessingParameter.AdcMax => "adcMax",
            ProcessingParameter.Invert => "invert",
            ProcessingParameter.EmaAlpha => "emaAlpha",
            ProcessingParameter.HysteresisPercent => "hysteresisPercent",
            ProcessingParameter.MaxBrightnessStepPercent => "maxBrightnessStepPercent",
            ProcessingParameter.Gamma => "gamma",
            _ => parameter.ToString()
        };
    }

    private static string GetProcessingModalDescription(ProcessingParameter parameter, Localizer localizer)
    {
        return parameter switch
        {
            ProcessingParameter.AdcMin => localizer["processing.adcMin.help"],
            ProcessingParameter.AdcMax => localizer["processing.adcMax.help"],
            ProcessingParameter.Invert => localizer["processing.invert.help"],
            ProcessingParameter.EmaAlpha => localizer["processing.emaAlpha.help"],
            ProcessingParameter.HysteresisPercent => localizer["processing.hysteresis.help"],
            ProcessingParameter.MaxBrightnessStepPercent => localizer["processing.step.help"],
            ProcessingParameter.Gamma => localizer["processing.gamma.help"],
            _ => string.Empty
        };
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Text = text };
        button.Accepted += (_, _) => action();
        return button;
    }

    private static Scheme CreateScheme(
        Color foreground,
        Color background,
        Color focusForeground,
        Color focusBackground,
        Color? hotForeground = null)
    {
        return new Scheme
        {
            Normal = new GuiAttribute(foreground, background),
            Focus = new GuiAttribute(focusForeground, focusBackground),
            Active = new GuiAttribute(focusForeground, focusBackground),
            HotNormal = new GuiAttribute(hotForeground ?? Color.BrightGreen, background),
            HotFocus = new GuiAttribute(focusForeground, focusBackground)
        };
    }

    private static void RegisterSchemes()
    {
        SchemeManager.AddScheme(BaseSchemeName, BaseScheme);
        SchemeManager.AddScheme(TitleSchemeName, TitleScheme);
        SchemeManager.AddScheme(AccentSchemeName, AccentScheme);
        SchemeManager.AddScheme(MutedSchemeName, MutedScheme);
        SchemeManager.AddScheme(CardSchemeName, CardScheme);
        SchemeManager.AddScheme(ButtonSchemeName, ButtonScheme);
        SchemeManager.AddScheme(ActiveButtonSchemeName, ActiveButtonScheme);
        SchemeManager.AddScheme(TabSchemeName, TabScheme);
        SchemeManager.AddScheme(ActiveTabSchemeName, ActiveTabScheme);
        SchemeManager.AddScheme(ModalSchemeName, ModalScheme);
    }

    private static string BuildCalibrationText(DashboardSnapshot snapshot, Localizer localizer)
    {
        var lines = new List<string>
        {
            localizer["calibration.explain.1"],
            localizer["calibration.explain.2"],
            string.Empty,
            localizer["calibration.explain.3"],
            localizer["calibration.explain.4"],
            localizer["calibration.explain.5"],
            localizer["calibration.explain.6"],
            string.Empty,
            $"{localizer["status.calibration"]}: {snapshot.CalibrationStatus}"
        };

        if (snapshot.CalibrationWizardStep == CalibrationWizardStep.Review)
        {
            lines.Add(string.Empty);
            lines.Add(snapshot.CalibrationTargetMode == CalibrationTargetMode.ManualTarget
                ? $"{localizer["calibration.review"]}: {localizer["calibration.manualValue"]} {snapshot.CalibrationManualInputBuffer}%."
                : $"{localizer["calibration.review"]}: {localizer["calibration.current"]}.");
        }

        if (snapshot.CalibrationWizardStep == CalibrationWizardStep.Queued)
        {
            lines.Add(string.Empty);
            lines.Add(localizer["calibration.queued"]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildGeneralText(DashboardSnapshot snapshot, Localizer localizer)
    {
        return localizer["settings.language"] + Environment.NewLine +
               localizer["settings.language.help.1"] + Environment.NewLine +
               localizer["settings.language.help.2"] + Environment.NewLine +
               Environment.NewLine +
               $"{localizer["settings.current"]}: {GetLanguageDisplayName(snapshot.Language)}";
    }

    private static string BuildAutostartText(DashboardSnapshot snapshot, Localizer localizer)
    {
        return $"{localizer["settings.autostart"]}: {FormatEnabled(snapshot.AutostartEnabled, localizer)}";
    }

    private static string FormatEnabled(bool enabled, Localizer localizer)
    {
        return enabled ? localizer["value.enabled"] : localizer["value.disabled"];
    }

    private static string BuildResponseText(DashboardSnapshot snapshot, Localizer localizer)
    {
        return localizer["settings.response.help.1"] + Environment.NewLine +
               localizer["settings.response.help.2"] + Environment.NewLine +
               Environment.NewLine +
               $"adcMin: {snapshot.ProcessingAdcMin?.ToString(CultureInfo.InvariantCulture) ?? localizer["value.none"]}" + Environment.NewLine +
               $"adcMax: {snapshot.ProcessingAdcMax?.ToString(CultureInfo.InvariantCulture) ?? localizer["value.none"]}" + Environment.NewLine +
               $"invert: {snapshot.ProcessingInvert?.ToString() ?? localizer["value.none"]}" + Environment.NewLine +
               $"emaAlpha: {FormatNullableNumber(snapshot.ProcessingEmaAlpha) ?? localizer["value.none"]}" + Environment.NewLine +
               $"hysteresisPercent: {snapshot.ProcessingHysteresisPercent?.ToString(CultureInfo.InvariantCulture) ?? localizer["value.none"]}" + Environment.NewLine +
               $"maxBrightnessStepPercent: {snapshot.ProcessingMaxBrightnessStepPercent?.ToString(CultureInfo.InvariantCulture) ?? localizer["value.none"]}" + Environment.NewLine +
               $"gamma: {FormatNullableNumber(snapshot.ProcessingGamma) ?? localizer["value.none"]}";
    }

    private static string GetLanguageDisplayName(UiLanguage language)
    {
        return language switch
        {
            UiLanguage.Russian => "Русский",
            UiLanguage.Spanish => "Español",
            _ => "English"
        };
    }

    private static string? FormatNullableNumber(double? value)
    {
        return value?.ToString("0.0###############", CultureInfo.InvariantCulture);
    }

    private static string BuildEventsText(DashboardSnapshot snapshot, Localizer localizer)
    {
        if (snapshot.Events.Count == 0)
        {
            return localizer["events.empty"];
        }

        return string.Join(
            Environment.NewLine,
            snapshot.Events.TakeLast(18).Select(entry =>
                $"{entry.Timestamp.LocalDateTime:HH:mm:ss}  {entry.Severity,-7}  {entry.Message}"));
    }

    private static string BuildDiagnosticsText(DashboardSnapshot snapshot)
    {
        var sensor = snapshot.LatestSensor;
        var sensorLines = sensor is null
            ? "Telemetry: waiting"
            : $"deviceId: {sensor.DeviceId}{Environment.NewLine}" +
              $"sensorId: {sensor.SensorId}{Environment.NewLine}" +
              $"value: {sensor.Value}{Environment.NewLine}" +
              $"raw: {sensor.Raw?.ToString() ?? "null"}{Environment.NewLine}" +
              $"calibrated: {sensor.Calibrated}{Environment.NewLine}" +
              $"received: {sensor.ReceivedAt.LocalDateTime:HH:mm:ss}";

        var monitorLines = snapshot.Monitors.Count == 0
            ? "Monitors: none"
            : string.Join(Environment.NewLine, snapshot.Monitors.Select(monitor =>
                $"{monitor.Source}/{monitor.Name}: {monitor.LastStatus}, applied={monitor.LastAppliedBrightness?.ToString() ?? "n/a"}"));

        return $"{sensorLines}{Environment.NewLine}{Environment.NewLine}" +
               $"Port: {snapshot.PortName ?? "n/a"} @ {snapshot.BaudRate?.ToString() ?? "n/a"}{Environment.NewLine}" +
               $"Profile: {snapshot.ProfileId ?? "n/a"}{Environment.NewLine}" +
               $"Measurement: {snapshot.MeasurementKind ?? "unknown"}{Environment.NewLine}{Environment.NewLine}" +
               monitorLines;
    }

    private static int? GetNormalizedSensorPercent(DashboardSnapshot snapshot)
    {
        if (snapshot.LatestSensor is null)
        {
            return null;
        }

        var max = string.Equals(snapshot.MeasurementKind, "Normalized1000", StringComparison.OrdinalIgnoreCase)
            ? 1000
            : 4095;
        var clamped = Math.Clamp(snapshot.LatestSensor.Value, 0, max);
        return (int)Math.Round(clamped * 100.0 / max, MidpointRounding.AwayFromZero);
    }

    private static string GetSunState(int? normalizedPercent)
    {
        return normalizedPercent switch
        {
            null => "WAITING",
            < 15 => "DARK",
            < 35 => "LOW",
            < 65 => "MID",
            < 90 => "BRIGHT",
            _ => "MAX"
        };
    }

    private static string[] BuildSunArt(int? normalizedPercent)
    {
        return GetSunState(normalizedPercent) switch
        {
            "DARK" =>
            [
                " .-. ",
                "(   )",
                " '-' "
            ],
            "LOW" =>
            [
                "   .   ",
                " \\.-./ ",
                " (   ) ",
                " /'-'\\ ",
                "   '   "
            ],
            "MID" =>
            [
                "    |    ",
                " \\.-./  ",
                "-- (   ) --",
                " /'-'\\  ",
                "    |    "
            ],
            "BRIGHT" =>
            [
                " \\  |  / ",
                "  \\.-./  ",
                "--- (   ) ---",
                "  /'-'\\  ",
                " /  |  \\ "
            ],
            "MAX" =>
            [
                " \\    |    / ",
                "---\\.-./---",
                "===(   )===",
                "---/'-'\\---",
                " /    |    \\ "
            ],
            _ =>
            [
                "  \\ | /  ",
                "   .-.   ",
                "-- ( ? ) --",
                "   '-'   ",
                "  / | \\  "
            ]
        };
    }

    private static string BuildBrightnessBar(int? normalizedPercent)
    {
        const int total = 32;
        var filled = normalizedPercent.HasValue
            ? Math.Clamp((int)Math.Round(normalizedPercent.Value / 100.0 * total, MidpointRounding.AwayFromZero), 0, total)
            : 0;

        return $"[{new string('#', filled)}{new string('-', total - filled)}]";
    }
}
