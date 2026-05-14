using System.Globalization;

namespace GenICam.Net.GenApi;

internal static class FormulaEvaluator
{
    public static double Evaluate(string formula, IReadOnlyDictionary<string, string> variables, NodeMap nodeMap)
    {
        var parser = new Parser(formula, variables, nodeMap);
        var value = parser.ParseConditional();
        parser.EnsureComplete();
        return value;
    }

    private sealed class Parser
    {
        private readonly string _formula;
        private readonly IReadOnlyDictionary<string, string> _variables;
        private readonly NodeMap _nodeMap;
        private int _position;
        private int _skipEvaluationDepth;

        public Parser(string formula, IReadOnlyDictionary<string, string> variables, NodeMap nodeMap)
        {
            _formula = formula;
            _variables = variables;
            _nodeMap = nodeMap;
        }

        public double ParseConditional()
        {
            var condition = ParseLogicalOr();
            SkipWhitespace();
            if (!Match('?'))
                return condition;

            var evaluateTrueBranch = IsTrue(condition);
            var whenTrue = ParseMaybeSkipping(!evaluateTrueBranch, ParseConditional);
            SkipWhitespace();
            if (!Match(':'))
                throw new FormatException("Formula conditional expression is missing ':'.");
            var whenFalse = ParseMaybeSkipping(evaluateTrueBranch, ParseConditional);

            return evaluateTrueBranch ? whenTrue : whenFalse;
        }

        private double ParseLogicalOr()
        {
            var value = ParseLogicalAnd();
            while (true)
            {
                SkipWhitespace();
                if (Match("||"))
                {
                    var right = ParseLogicalAnd();
                    value = IsTrue(value) || IsTrue(right) ? 1 : 0;
                }
                else
                    return value;
            }
        }

        private double ParseLogicalAnd()
        {
            var value = ParseBitwiseOr();
            while (true)
            {
                SkipWhitespace();
                if (Match("&&"))
                {
                    var right = ParseBitwiseOr();
                    value = IsTrue(value) && IsTrue(right) ? 1 : 0;
                }
                else
                    return value;
            }
        }

        private double ParseBitwiseOr()
        {
            var value = ParseBitwiseXor();
            while (true)
            {
                SkipWhitespace();
                if (Peek("||"))
                    return value;
                if (Match('|'))
                    value = ToLong(value) | ToLong(ParseBitwiseXor());
                else
                    return value;
            }
        }

        private double ParseBitwiseXor()
        {
            var value = ParseBitwiseAnd();
            while (true)
            {
                SkipWhitespace();
                if (Match('^'))
                    value = ToLong(value) ^ ToLong(ParseBitwiseAnd());
                else
                    return value;
            }
        }

        private double ParseBitwiseAnd()
        {
            var value = ParseEquality();
            while (true)
            {
                SkipWhitespace();
                if (Peek("&&"))
                    return value;
                if (Match('&'))
                    value = ToLong(value) & ToLong(ParseEquality());
                else
                    return value;
            }
        }

        private double ParseEquality()
        {
            var value = ParseRelational();
            while (true)
            {
                SkipWhitespace();
                if (Match("=="))
                    value = AreEqual(value, ParseRelational()) ? 1 : 0;
                else if (Match("<>"))
                    value = !AreEqual(value, ParseRelational()) ? 1 : 0;
                else if (Match("!="))
                    value = !AreEqual(value, ParseRelational()) ? 1 : 0;
                else if (Match("="))
                    value = AreEqual(value, ParseRelational()) ? 1 : 0;
                else
                    return value;
            }
        }

        private double ParseRelational()
        {
            var value = ParseShift();
            while (true)
            {
                SkipWhitespace();
                if (Peek("<>"))
                    return value;
                if (Match("<="))
                    value = value <= ParseShift() ? 1 : 0;
                else if (Match(">="))
                    value = value >= ParseShift() ? 1 : 0;
                else if (Peek("<<") || Peek(">>"))
                    return value;
                else if (Match('<'))
                    value = value < ParseShift() ? 1 : 0;
                else if (Match('>'))
                    value = value > ParseShift() ? 1 : 0;
                else
                    return value;
            }
        }

        private double ParseShift()
        {
            var value = ParseAdditive();
            while (true)
            {
                SkipWhitespace();
                if (Match("<<"))
                    value = ToLong(value) << checked((int)ToLong(ParseAdditive()));
                else if (Match(">>"))
                    value = ToLong(value) >> checked((int)ToLong(ParseAdditive()));
                else
                    return value;
            }
        }

        private double ParseAdditive()
        {
            var value = ParseTerm();
            while (true)
            {
                SkipWhitespace();
                if (Match('+'))
                    value += ParseTerm();
                else if (Match('-'))
                    value -= ParseTerm();
                else
                    return value;
            }
        }

        public void EnsureComplete()
        {
            SkipWhitespace();
            if (_position != _formula.Length)
                throw new FormatException($"Unexpected token in formula at position {_position}.");
        }

        private double ParseTerm()
        {
            var value = ParsePower();
            while (true)
            {
                SkipWhitespace();
                if (Peek("**"))
                    return value;
                if (Match('*'))
                    value *= ParsePower();
                else if (Match('/'))
                    value /= ParsePower();
                else if (Match('%'))
                    value = ToLong(value) % ToLong(ParsePower());
                else
                    return value;
            }
        }

        private double ParsePower()
        {
            var value = ParseFactor();
            SkipWhitespace();
            if (Match("**"))
                value = Math.Pow(value, ParsePower());

            return value;
        }

        private double ParseFactor()
        {
            SkipWhitespace();
            if (Match('+'))
                return ParseFactor();
            if (Match('-'))
                return -ParseFactor();
            if (Match('!'))
                return IsTrue(ParseFactor()) ? 0 : 1;
            if (Match('~'))
                return ~ToLong(ParseFactor());
            if (Match('('))
            {
                var value = ParseConditional();
                SkipWhitespace();
                if (!Match(')'))
                    throw new FormatException($"Formula contains an unmatched '(' at position {_position}.");
                return value;
            }
            if (_position < _formula.Length && (char.IsLetter(_formula[_position]) || _formula[_position] == '_'))
            {
                var identifier = ParseIdentifier();
                SkipWhitespace();
                if (Match('('))
                    return EvaluateFunction(identifier, ParseArguments());

                return ResolveVariable(identifier);
            }

            return ParseNumber();
        }

        private double[] ParseArguments()
        {
            var args = new List<double>();
            SkipWhitespace();
            if (Match(')'))
                return [];

            while (true)
            {
                args.Add(ParseConditional());
                SkipWhitespace();
                if (Match(')'))
                    return args.ToArray();
                if (!Match(','))
                    throw new FormatException($"Formula function call is missing ',' or ')' at position {_position}.");
            }
        }

        private double ParseNumber()
        {
            SkipWhitespace();
            if (Peek("0x") || Peek("0X"))
            {
                _position += 2;
                var hexStart = _position;
                while (_position < _formula.Length && Uri.IsHexDigit(_formula[_position]))
                    _position++;

                if (hexStart == _position)
                    throw new FormatException($"Expected hexadecimal digits at position {hexStart}.");

                return Convert.ToInt64(_formula[hexStart.._position], 16);
            }

            var start = _position;
            while (_position < _formula.Length &&
                   (char.IsDigit(_formula[_position]) || _formula[_position] is '.' or 'e' or 'E' or '+' or '-'))
            {
                if ((_formula[_position] is '+' or '-') && _position != start &&
                    _formula[_position - 1] is not ('e' or 'E'))
                {
                    break;
                }
                _position++;
            }

            if (start == _position)
                throw new FormatException($"Expected number at position {_position}.");

            return double.Parse(_formula[start.._position], CultureInfo.InvariantCulture);
        }

        private string ParseIdentifier()
        {
            var start = _position;
            while (_position < _formula.Length && (char.IsLetterOrDigit(_formula[_position]) || _formula[_position] == '_'))
                _position++;

            return _formula[start.._position];
        }

        private double ResolveVariable(string variableName)
        {
            if (IsSkippingEvaluation)
                return 0;

            if (variableName.Equals("true", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (variableName.Equals("false", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (!_variables.TryGetValue(variableName, out var nodeName))
                throw new KeyNotFoundException($"Formula variable '{variableName}' is not defined.");

            return _nodeMap.GetNode(nodeName) switch
            {
                IInteger integer => integer.Value,
                IFloat floating => floating.Value,
                IEnumeration enumeration => enumeration.IntValue,
                IBoolean boolean => boolean.Value ? 1 : 0,
                null => throw new KeyNotFoundException($"Formula variable '{variableName}' references missing node '{nodeName}'."),
                var node => throw new InvalidOperationException($"Node '{node.Name}' cannot be used as a numeric formula variable."),
            };
        }

        private double ParseMaybeSkipping(bool skipEvaluation, Func<double> parse)
        {
            if (!skipEvaluation)
                return parse();

            _skipEvaluationDepth++;
            try
            {
                _ = parse();
                return 0;
            }
            finally
            {
                _skipEvaluationDepth--;
            }
        }

        private static double EvaluateFunction(string name, double[] args)
        {
            var normalized = name.ToUpperInvariant();
            switch (normalized)
            {
                case "ABS":
                    RequireArgs(name, args, 1);
                    return Math.Abs(args[0]);
                case "ATAN":
                    RequireArgs(name, args, 1);
                    return Math.Atan(args[0]);
                case "CEIL":
                case "CEILING":
                    RequireArgs(name, args, 1);
                    return Math.Ceiling(args[0]);
                case "COS":
                    RequireArgs(name, args, 1);
                    return Math.Cos(args[0]);
                case "EXP":
                    RequireArgs(name, args, 1);
                    return Math.Exp(args[0]);
                case "FLOOR":
                    RequireArgs(name, args, 1);
                    return Math.Floor(args[0]);
                case "INT":
                    RequireArgs(name, args, 1);
                    return Math.Truncate(args[0]);
                case "LN":
                    RequireArgs(name, args, 1);
                    return Math.Log(args[0]);
                case "LOG":
                case "LOG10":
                    RequireArgs(name, args, 1);
                    return Math.Log10(args[0]);
                case "MAX":
                    RequireArgs(name, args, 2);
                    return Math.Max(args[0], args[1]);
                case "MIN":
                    RequireArgs(name, args, 2);
                    return Math.Min(args[0], args[1]);
                case "POW":
                    RequireArgs(name, args, 2);
                    return Math.Pow(args[0], args[1]);
                case "ROUND":
                    RequireArgs(name, args, 1);
                    return Math.Round(args[0]);
                case "SGN":
                case "SIGN":
                    RequireArgs(name, args, 1);
                    return Math.Sign(args[0]);
                case "SIN":
                    RequireArgs(name, args, 1);
                    return Math.Sin(args[0]);
                case "SQRT":
                    RequireArgs(name, args, 1);
                    return Math.Sqrt(args[0]);
                case "TAN":
                    RequireArgs(name, args, 1);
                    return Math.Tan(args[0]);
                default:
                    throw new NotSupportedException($"Formula function '{name}' is not supported.");
            }
        }

        private bool Match(char value)
        {
            if (_position >= _formula.Length || _formula[_position] != value)
                return false;

            _position++;
            return true;
        }

        private bool Match(string value)
        {
            if (!Peek(value))
                return false;

            _position += value.Length;
            return true;
        }

        private bool Peek(string value)
            => _formula.AsSpan(_position).StartsWith(value, StringComparison.Ordinal);

        private void SkipWhitespace()
        {
            while (_position < _formula.Length && char.IsWhiteSpace(_formula[_position]))
                _position++;
        }

        private static bool IsTrue(double value)
            => Math.Abs(value) > double.Epsilon;

        private static bool AreEqual(double left, double right)
            => Math.Abs(left - right) <= double.Epsilon;

        private static long ToLong(double value)
            => checked((long)value);

        private bool IsSkippingEvaluation => _skipEvaluationDepth > 0;

        private static void RequireArgs(string name, double[] args, int expectedCount)
        {
            if (args.Length != expectedCount)
                throw new ArgumentException($"Formula function '{name}' expects {expectedCount} argument(s), got {args.Length}.");
        }
    }
}
