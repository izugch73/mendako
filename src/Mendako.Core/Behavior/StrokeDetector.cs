namespace Mendako.Core.Behavior;

/// <summary>
/// カーソルがメンダコの上を左右に往復したら「なでた」とみなす。
/// ボタンを押さないので、クリック (つつく) やドラッグ (つまむ) と取り違えない。
/// </summary>
public sealed class StrokeDetector
{
    /// <summary>ひとなでとして数える最小の移動量。手ぶれや通りすがりを拾わないためのもの。</summary>
    public const double MinLegLength = 8d;

    /// <summary>行って、戻って、もう一度行く。通り過ぎただけでは 1 回にしかならない。</summary>
    public const int RequiredLegs = 3;

    /// <summary>この秒数のあいだ折り返しがなければ、数え直す。</summary>
    public const double LegTimeoutSeconds = 0.9d;

    /// <summary>なでたと判定してから、次を数え始めるまでの間。なで続けても連射にならない。</summary>
    public const double CooldownSeconds = 1.2d;

    private double _anchor;
    private double _extreme;
    private double _lastLegAt;
    private double _readyAt;
    private int _direction;
    private int _legs;
    private bool _tracking;

    /// <summary>カーソルの位置を 1 回ぶん渡す。なでたと判定した瞬間だけ true を返す。</summary>
    /// <param name="time">単調に増える時刻（秒）。</param>
    /// <param name="x">カーソルの横位置。単位は <see cref="MinLegLength"/> と揃っていれば何でもよい。</param>
    /// <param name="overPet">ボタンを押さずにメンダコの上にいるか。</param>
    public bool Update(double time, double x, bool overPet)
    {
        if (!overPet || time < _readyAt)
        {
            _tracking = false;
            return false;
        }

        if (!_tracking || time - _lastLegAt > LegTimeoutSeconds)
        {
            _tracking = true;
            _anchor = x;
            _direction = 0;
            _legs = 0;
            _lastLegAt = time;
            return false;
        }

        if (_direction == 0)
        {
            if (Math.Abs(x - _anchor) < MinLegLength)
            {
                return false;
            }

            _direction = Math.Sign(x - _anchor);
            StartLeg(time, x);
        }
        else if ((x - _extreme) * _direction > 0d)
        {
            // 同じ向きに進み続けている
            _extreme = x;
        }
        else if ((_extreme - x) * _direction >= MinLegLength)
        {
            // 折り返した
            _direction = -_direction;
            StartLeg(time, x);
        }

        if (_legs < RequiredLegs)
        {
            return false;
        }

        _tracking = false;
        _readyAt = time + CooldownSeconds;
        return true;
    }

    private void StartLeg(double time, double x)
    {
        _legs++;
        _extreme = x;
        _lastLegAt = time;
    }
}
