using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingLiveAiTimerController
{
    private readonly ToggleButton _button;
    private readonly EventHandler _analysisTick;
    private readonly Func<bool> _canBlink;
    private DispatcherTimer? _analysisTimer;
    private DispatcherTimer? _blinkTimer;
    private bool _blinkState;

    public CodingLiveAiTimerController(
        ToggleButton button,
        EventHandler analysisTick,
        Func<bool> canBlink)
    {
        _button = button ?? throw new ArgumentNullException(nameof(button));
        _analysisTick = analysisTick ?? throw new ArgumentNullException(nameof(analysisTick));
        _canBlink = canBlink ?? throw new ArgumentNullException(nameof(canBlink));
    }

    public bool IsAnalysisTimerRunning => _analysisTimer?.IsEnabled == true;

    public bool IsBlinkTimerRunning => _blinkTimer?.IsEnabled == true;

    public void Start()
    {
        StopTimers();

        _analysisTimer = new DispatcherTimer { Interval = CodingLiveAiTimerSettings.AnalysisInterval };
        _analysisTimer.Tick += _analysisTick;
        _analysisTimer.Start();

        _blinkState = false;
        _blinkTimer = new DispatcherTimer { Interval = CodingLiveAiTimerSettings.BlinkInterval };
        _blinkTimer.Tick += (_, _) =>
        {
            if (!_canBlink())
                return;

            _blinkState = !_blinkState;
            _button.Background = new SolidColorBrush(
                CodingLiveAiButtonDisplayPolicy.BlinkColor(_blinkState));
        };
        _blinkTimer.Start();

        _button.Background = new SolidColorBrush(CodingLiveAiButtonDisplayPolicy.ActiveColor);
    }

    public void Stop(bool resetButton)
    {
        StopTimers();
        if (resetButton)
            _button.ClearValue(Control.BackgroundProperty);
    }

    public void StopTimers()
    {
        if (_analysisTimer != null)
        {
            _analysisTimer.Tick -= _analysisTick;
            _analysisTimer.Stop();
            _analysisTimer = null;
        }

        if (_blinkTimer != null)
        {
            _blinkTimer.Stop();
            _blinkTimer = null;
        }
    }
}
