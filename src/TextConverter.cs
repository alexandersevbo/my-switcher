using System.Text.RegularExpressions;

namespace MySwitcher;

public enum LayoutDirection
{
    ToRussian,
    ToEnglish,
}

/// <summary>Чистые преобразования текста: смена раскладки и инверсия регистра.</summary>
public static class TextConverter
{
    // Символы на одних и тех же клавишах: QWERTY (US) и ЙЦУКЕН (русская).
    private const string En =
        "`qwertyuiop[]asdfghjkl;'zxcvbnm,./" +
        "~QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?" +
        "@#$^&|";

    private const string Ru =
        "ёйцукенгшщзхъфывапролджэячсмитьбю." +
        "ЁЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ," +
        "\"№;:?/";

    private static readonly Dictionary<char, char> EnToRu = Build(En, Ru);
    private static readonly Dictionary<char, char> RuToEn = Build(Ru, En);

    private static Dictionary<char, char> Build(string from, string to)
    {
        if (from.Length != to.Length)
            throw new InvalidOperationException("Таблицы раскладок разной длины.");
        var map = new Dictionary<char, char>(from.Length);
        for (int i = 0; i < from.Length; i++)
            map.Add(from[i], to[i]);
        return map;
    }

    /// <summary>
    /// Куда переводить: если кириллических букв больше — в английскую, если латинских — в русскую.
    /// При равенстве решает текущая раскладка (текст набран в ней).
    /// </summary>
    public static LayoutDirection DetectDirection(string text, bool currentLayoutIsRussian)
    {
        int cyr = 0, lat = 0;
        foreach (char c in text)
        {
            if (c is >= 'а' and <= 'я' or >= 'А' and <= 'Я' or 'ё' or 'Ё') cyr++;
            else if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') lat++;
        }

        if (cyr != lat)
            return cyr > lat ? LayoutDirection.ToEnglish : LayoutDirection.ToRussian;
        return currentLayoutIsRussian ? LayoutDirection.ToEnglish : LayoutDirection.ToRussian;
    }

    public static string ConvertLayout(string text, LayoutDirection direction)
    {
        var map = direction == LayoutDirection.ToRussian ? EnToRu : RuToEn;
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (map.TryGetValue(chars[i], out char mapped))
                chars[i] = mapped;
        }
        return new string(chars);
    }

    public static string InvertCase(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsUpper(c)) chars[i] = char.ToLowerInvariant(c);
            else if (char.IsLower(c)) chars[i] = char.ToUpperInvariant(c);
        }
        return new string(chars);
    }

    /// <summary>
    /// Типичный след набора с включённым CapsLock: слово из строчной буквы
    /// и дальше только заглавных («пРИВЕТ», «hELLO»).
    /// </summary>
    public static bool LooksLikeCapsTyped(string text) => CapsTypedWord.IsMatch(text);

    private static readonly Regex CapsTypedWord = new(@"(?<!\p{L})\p{Ll}\p{Lu}{2,}(?!\p{L})");
}
