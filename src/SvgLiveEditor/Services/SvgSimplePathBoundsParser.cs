using System.Globalization;
using SvgLiveEditor.Models;

namespace SvgLiveEditor.Services;

internal static class SvgSimplePathBoundsParser
{
    public static bool TryParsePath(
        string value,
        out SvgVisualBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(value);
        bounds = default;
        NumberScanner scanner = new(value);
        BoundsBuilder builder = new();
        char command = '\0';
        double currentX = 0;
        double currentY = 0;
        double subpathX = 0;
        double subpathY = 0;
        bool hasCurrentPoint = false;

        while (!scanner.IsAtEnd)
        {
            if (scanner.TryReadCommand(out char nextCommand))
            {
                command = nextCommand;
            }
            else if (command == '\0')
            {
                return false;
            }

            bool relative = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    if (!scanner.TryReadNumber(out double moveX)
                        || !scanner.TryReadNumber(out double moveY))
                    {
                        return false;
                    }
                    if (relative)
                    {
                        moveX += currentX;
                        moveY += currentY;
                    }
                    currentX = moveX;
                    currentY = moveY;
                    subpathX = currentX;
                    subpathY = currentY;
                    hasCurrentPoint = true;
                    builder.Include(currentX, currentY);
                    command = relative ? 'l' : 'L';
                    break;

                case 'L':
                    if (!hasCurrentPoint
                        || !scanner.TryReadNumber(out double lineX)
                        || !scanner.TryReadNumber(out double lineY))
                    {
                        return false;
                    }
                    if (relative)
                    {
                        lineX += currentX;
                        lineY += currentY;
                    }
                    currentX = lineX;
                    currentY = lineY;
                    builder.Include(currentX, currentY);
                    break;

                case 'H':
                    if (!hasCurrentPoint
                        || !scanner.TryReadNumber(out double horizontal))
                    {
                        return false;
                    }
                    currentX = relative
                        ? currentX + horizontal
                        : horizontal;
                    builder.Include(currentX, currentY);
                    break;

                case 'V':
                    if (!hasCurrentPoint
                        || !scanner.TryReadNumber(out double vertical))
                    {
                        return false;
                    }
                    currentY = relative
                        ? currentY + vertical
                        : vertical;
                    builder.Include(currentX, currentY);
                    break;

                case 'Z':
                    if (!hasCurrentPoint)
                    {
                        return false;
                    }
                    currentX = subpathX;
                    currentY = subpathY;
                    builder.Include(currentX, currentY);
                    command = '\0';
                    break;

                default:
                    return false;
            }
        }

        return builder.TryBuild(out bounds);
    }

    public static bool TryParsePoints(
        string value,
        out SvgVisualBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(value);
        bounds = default;
        NumberScanner scanner = new(value);
        BoundsBuilder builder = new();
        while (!scanner.IsAtEnd)
        {
            if (!scanner.TryReadNumber(out double x)
                || !scanner.TryReadNumber(out double y))
            {
                return false;
            }
            builder.Include(x, y);
        }

        return builder.TryBuild(out bounds);
    }

    private sealed class NumberScanner
    {
        private readonly string _value;
        private int _offset;

        public NumberScanner(string value)
        {
            _value = value;
        }

        public bool IsAtEnd
        {
            get
            {
                SkipSeparators();
                return _offset >= _value.Length;
            }
        }

        public bool TryReadCommand(out char command)
        {
            SkipSeparators();
            if (_offset < _value.Length && char.IsLetter(_value[_offset]))
            {
                command = _value[_offset++];
                return true;
            }

            command = '\0';
            return false;
        }

        public bool TryReadNumber(out double number)
        {
            number = 0;
            SkipSeparators();
            int start = _offset;
            if (_offset < _value.Length
                && _value[_offset] is '+' or '-')
            {
                _offset++;
            }

            bool hasDigits = ReadDigits();
            if (_offset < _value.Length && _value[_offset] == '.')
            {
                _offset++;
                hasDigits |= ReadDigits();
            }
            if (!hasDigits)
            {
                _offset = start;
                return false;
            }

            if (_offset < _value.Length
                && _value[_offset] is 'e' or 'E')
            {
                int exponentStart = _offset++;
                if (_offset < _value.Length
                    && _value[_offset] is '+' or '-')
                {
                    _offset++;
                }
                if (!ReadDigits())
                {
                    _offset = exponentStart;
                }
            }

            return double.TryParse(
                    _value.AsSpan(start, _offset - start),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number)
                && double.IsFinite(number)
                && Math.Abs(number)
                    <= SvgVisualLengthParser.MaximumAbsoluteValue;
        }

        private bool ReadDigits()
        {
            int start = _offset;
            while (_offset < _value.Length
                   && char.IsAsciiDigit(_value[_offset]))
            {
                _offset++;
            }
            return _offset > start;
        }

        private void SkipSeparators()
        {
            while (_offset < _value.Length
                   && (char.IsWhiteSpace(_value[_offset])
                       || _value[_offset] == ','))
            {
                _offset++;
            }
        }
    }

    private sealed class BoundsBuilder
    {
        private bool _hasPoint;
        private double _left;
        private double _top;
        private double _right;
        private double _bottom;

        public void Include(double x, double y)
        {
            if (!_hasPoint)
            {
                _left = _right = x;
                _top = _bottom = y;
                _hasPoint = true;
                return;
            }

            _left = Math.Min(_left, x);
            _top = Math.Min(_top, y);
            _right = Math.Max(_right, x);
            _bottom = Math.Max(_bottom, y);
        }

        public bool TryBuild(out SvgVisualBounds bounds)
        {
            bounds = new SvgVisualBounds(
                _left,
                _top,
                _right,
                _bottom);
            return _hasPoint;
        }
    }
}
