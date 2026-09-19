namespace SprintPilot.Application;

public sealed record LocalPublicHoliday(string Name, DateOnly Date);

public static class PublicHolidays
{
    public static LocalPublicHoliday[] For(string calendar, int year)
    {
        var easter = EasterSunday(year);
        IEnumerable<LocalPublicHoliday> rows = calendar.ToUpperInvariant() switch
        {
            "BE" => [
                H("New Year's Day", year, 1, 1), H("Easter Monday", easter.AddDays(1)),
                H("Labour Day", year, 5, 1), H("Ascension Day", easter.AddDays(39)),
                H("Whit Monday", easter.AddDays(50)), H("Belgian National Day", year, 7, 21),
                H("Assumption Day", year, 8, 15), H("All Saints' Day", year, 11, 1),
                H("Armistice Day", year, 11, 11), H("Christmas Day", year, 12, 25)
            ],
            "DE" => [
                H("New Year's Day", year, 1, 1), H("Good Friday", easter.AddDays(-2)),
                H("Easter Monday", easter.AddDays(1)), H("Labour Day", year, 5, 1),
                H("Ascension Day", easter.AddDays(39)), H("Whit Monday", easter.AddDays(50)),
                H("German Unity Day", year, 10, 3), H("Christmas Day", year, 12, 25),
                H("Second Day of Christmas", year, 12, 26)
            ],
            "PL" => [
                H("New Year's Day", year, 1, 1), H("Epiphany", year, 1, 6),
                H("Easter Sunday", easter), H("Easter Monday", easter.AddDays(1)),
                H("Labour Day", year, 5, 1), H("Constitution Day", year, 5, 3),
                H("Pentecost", easter.AddDays(49)), H("Corpus Christi", easter.AddDays(60)),
                H("Assumption Day", year, 8, 15), H("All Saints' Day", year, 11, 1),
                H("Independence Day", year, 11, 11), H("Christmas Eve", year, 12, 24),
                H("Christmas Day", year, 12, 25), H("Second Day of Christmas", year, 12, 26)
            ],
            "CZ" => [
                H("Restoration Day / New Year's Day", year, 1, 1), H("Good Friday", easter.AddDays(-2)),
                H("Easter Monday", easter.AddDays(1)), H("Labour Day", year, 5, 1),
                H("Liberation Day", year, 5, 8), H("Saints Cyril and Methodius Day", year, 7, 5),
                H("Jan Hus Day", year, 7, 6), H("Czech Statehood Day", year, 9, 28),
                H("Independent Czechoslovak State Day", year, 10, 28), H("Struggle for Freedom and Democracy Day", year, 11, 17),
                H("Christmas Eve", year, 12, 24), H("Christmas Day", year, 12, 25),
                H("Second Day of Christmas", year, 12, 26)
            ],
            "CA" => [
                H("New Year's Day", year, 1, 1), H("Good Friday", easter.AddDays(-2)),
                H("Victoria Day", MondayOnOrBefore(new DateOnly(year, 5, 24))), H("Canada Day", year, 7, 1),
                H("Labour Day", NthWeekday(year, 9, DayOfWeek.Monday, 1)),
                H("National Day for Truth and Reconciliation", year, 9, 30),
                H("Thanksgiving", NthWeekday(year, 10, DayOfWeek.Monday, 2)),
                H("Remembrance Day", year, 11, 11), H("Christmas Day", year, 12, 25),
                H("Boxing Day", year, 12, 26)
            ],
            "CA-QC" => [
                H("New Year's Day", year, 1, 1), H("Good Friday", easter.AddDays(-2)),
                H("National Patriots' Day", MondayOnOrBefore(new DateOnly(year, 5, 24))),
                H("Québec National Holiday", year, 6, 24), H("Canada Day", year, 7, 1),
                H("Labour Day", NthWeekday(year, 9, DayOfWeek.Monday, 1)),
                H("Thanksgiving", NthWeekday(year, 10, DayOfWeek.Monday, 2)),
                H("Christmas Day", year, 12, 25)
            ],
            _ => []
        };
        return rows.GroupBy(h => h.Date).Select(g => g.First()).OrderBy(h => h.Date).ToArray();
    }

    static LocalPublicHoliday H(string name, int year, int month, int day) => new(name, new DateOnly(year, month, day));
    static LocalPublicHoliday H(string name, DateOnly day) => new(name, day);

    static DateOnly NthWeekday(int year, int month, DayOfWeek weekday, int nth)
    {
        var day = new DateOnly(year, month, 1);
        var delta = ((int)weekday - (int)day.DayOfWeek + 7) % 7;
        return day.AddDays(delta + (nth - 1) * 7);
    }

    static DateOnly MondayOnOrBefore(DateOnly day)
    {
        var delta = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return day.AddDays(-delta);
    }

    // Meeus/Jones/Butcher Gregorian Easter algorithm.
    static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }
}
