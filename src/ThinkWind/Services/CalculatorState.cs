using System;
using System.Globalization;

namespace OrbyLauncher.Services;
internal sealed class CalculatorState
{
    private string entry = "0";
    private decimal accumulator;
    private string? operation;
    private bool fresh = true;
    private string? error;
    internal string Display => error ?? entry.Replace('.', ',');
    internal string Expression => operation is null ? "" : accumulator.ToString("G29", CultureInfo.GetCultureInfo("pt-BR")) + " " + operation;
    private decimal Value => decimal.Parse(entry, NumberStyles.Float, CultureInfo.InvariantCulture);
    private void Set(decimal value) => entry = value.ToString("0.############################", CultureInfo.InvariantCulture);
    private void Clear() { entry = "0"; accumulator = 0; operation = null; error = null; fresh = true; }
    private static decimal Evaluate(decimal left, decimal right, string op) => op switch
    { "+" => left + right, "−" => left - right, "×" => left * right, "÷" => left / right, _ => right };
    internal void Press(string key)
    {
        if (key == "C") { Clear(); return; }
        if (error is not null) { if (key.Length == 1 && char.IsAsciiDigit(key[0])) Clear(); else return; }
        try
        {
            if (key.Length == 1 && char.IsAsciiDigit(key[0]))
            {
                if (fresh) { entry = "0"; fresh = false; }
                if (entry.Replace("-", "").Replace(".", "").Length >= 28) return;
                entry = entry == "0" ? key : entry == "-0" ? "-" + key : entry + key;
                _ = Value; return;
            }
            switch (key)
            {
                case ",":
                    if (fresh) { entry = "0"; fresh = false; }
                    if (!entry.Contains('.')) entry += ".";
                    break;
                case "⌫":
                    if (fresh) { entry = "0"; fresh = false; }
                    else { entry = entry.Length > 1 ? entry[..^1] : "0"; if (entry == "-") entry = "0"; }
                    break;
                case "±": Set(-Value); break;
                case "%": Set(operation is "+" or "−" ? accumulator * (Value / 100) : Value / 100); fresh = false; break;
                case "+": case "−": case "×": case "÷":
                    accumulator = operation is not null && !fresh ? Evaluate(accumulator, Value, operation) : Value;
                    Set(accumulator); operation = key; fresh = true; break;
                case "=":
                    if (operation is not null) Set(Evaluate(accumulator, Value, operation));
                    operation = null; fresh = true; break;
            }
        }
        catch (DivideByZeroException) { error = "Divisão por zero"; operation = null; }
        catch (OverflowException) { error = "Limite numérico"; operation = null; }
    }
}
