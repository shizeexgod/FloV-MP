using System.Text;

namespace FloVMP.Core.Database;

/// <summary>
/// Разбиение .sql-файла на отдельные выражения.
///
/// Наивный <c>text.Split(';')</c> здесь недопустим: точка с запятой встречается
/// внутри строковых литералов (<c>DEFAULT 'a;b'</c>), внутри комментариев
/// (<c>-- см. пункт 3; важно</c>) и внутри <c>COMMENT '...'</c> у колонок —
/// такой сплит разрежет выражение посередине и миграция упадёт на половине
/// CREATE TABLE, оставив базу в полу-накатанном состоянии.
///
/// Поддерживается: строки в <c>'</c> и <c>"</c> (с экранированием <c>\</c> и
/// удвоением кавычки), идентификаторы в backtick'ах, построчные комментарии
/// <c>--</c> и <c>#</c>, блочные <c>/* */</c>.
/// </summary>
public static class SqlScriptSplitter
{
    public static IReadOnlyList<string> Split(string script)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(script)) return result;

        var sb = new StringBuilder(256);
        var i = 0;
        var n = script.Length;

        while (i < n)
        {
            var c = script[i];

            // Построчный комментарий: '-- ' или '#'. ВАЖНО: '--' считается
            // комментарием только если дальше пробел/конец строки — иначе
            // выражение вроде `a--1` (двойное отрицание) съелось бы целиком.
            if ((c == '-' && i + 1 < n && script[i + 1] == '-' &&
                 (i + 2 >= n || script[i + 2] == ' ' || script[i + 2] == '\t' || script[i + 2] == '\r' || script[i + 2] == '\n'))
                || c == '#')
            {
                while (i < n && script[i] != '\n') i++;
                continue; // сам комментарий в выражение не попадает
            }

            // Блочный комментарий.
            if (c == '/' && i + 1 < n && script[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < n && !(script[i] == '*' && script[i + 1] == '/')) i++;
                i = Math.Min(i + 2, n);
                sb.Append(' '); // комментарий работает как разделитель токенов
                continue;
            }

            // Литералы и идентификаторы — копируются как есть, ';' внутри не режет.
            if (c == '\'' || c == '"' || c == '`')
            {
                var quote = c;
                sb.Append(c);
                i++;
                while (i < n)
                {
                    var q = script[i];
                    if (q == '\\' && quote != '`' && i + 1 < n)
                    {
                        sb.Append(q).Append(script[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (q == quote)
                    {
                        // Удвоенная кавычка внутри литерала ('' / `` / "") — не конец.
                        if (i + 1 < n && script[i + 1] == quote)
                        {
                            sb.Append(q).Append(q);
                            i += 2;
                            continue;
                        }
                        sb.Append(q);
                        i++;
                        break;
                    }
                    sb.Append(q);
                    i++;
                }
                continue;
            }

            if (c == ';')
            {
                Flush(result, sb);
                i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        Flush(result, sb);
        return result;
    }

    private static void Flush(List<string> result, StringBuilder sb)
    {
        var stmt = sb.ToString().Trim();
        sb.Clear();
        if (stmt.Length > 0) result.Add(stmt);
    }
}
