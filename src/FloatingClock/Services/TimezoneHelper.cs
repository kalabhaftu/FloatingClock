using System;
using System.Collections.Generic;

namespace FloatingClock.Services;

/// <summary>Formats timezone IDs as friendly "City (UTC±X)" strings.</summary>
public static class TimezoneHelper
{
    private static readonly Dictionary<string, string> KnownCities = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Americas ──
        ["Eastern Standard Time"]           = "New York",
        ["US Eastern Standard Time"]        = "Indiana",
        ["Central Standard Time"]           = "Chicago",
        ["Mountain Standard Time"]          = "Denver",
        ["US Mountain Standard Time"]       = "Phoenix",
        ["Pacific Standard Time"]           = "Los Angeles",
        ["Alaskan Standard Time"]           = "Anchorage",
        ["Hawaiian Standard Time"]          = "Honolulu",
        ["Atlantic Standard Time"]          = "Halifax",
        ["Newfoundland Standard Time"]      = "St. John's",
        ["SA Pacific Standard Time"]        = "Bogota",
        ["Venezuela Standard Time"]         = "Caracas",
        ["E. South America Standard Time"]  = "Sao Paulo",
        ["Argentina Standard Time"]         = "Buenos Aires",
        ["Central America Standard Time"]   = "Guatemala",
        ["Canada Central Standard Time"]    = "Saskatchewan",
        ["Mexico Standard Time"]            = "Mexico City",
        ["Central Standard Time (Mexico)"]  = "Guadalajara",
        ["Pacific Standard Time (Mexico)"]  = "Tijuana",
        ["Mountain Standard Time (Mexico)"] = "Chihuahua",

        // ── Europe ──
        ["GMT Standard Time"]               = "London",
        ["Greenwich Standard Time"]         = "Reykjavik",
        ["W. Europe Standard Time"]         = "Berlin",
        ["Romance Standard Time"]           = "Paris",
        ["Central European Standard Time"]  = "Warsaw",
        ["Central Europe Standard Time"]    = "Prague",
        ["E. Europe Standard Time"]         = "Chisinau",
        ["FLE Standard Time"]               = "Helsinki",
        ["GTB Standard Time"]               = "Athens",
        ["Russian Standard Time"]           = "Moscow",
        ["Turkey Standard Time"]            = "Istanbul",
        ["Belarus Standard Time"]           = "Minsk",
        ["Kaliningrad Standard Time"]       = "Kaliningrad",

        // ── Asia ──
        ["Arabian Standard Time"]           = "Dubai",
        ["Arab Standard Time"]              = "Riyadh",
        ["India Standard Time"]             = "Mumbai",
        ["China Standard Time"]             = "Beijing",
        ["Tokyo Standard Time"]             = "Tokyo",
        ["Korea Standard Time"]             = "Seoul",
        ["Singapore Standard Time"]         = "Singapore",
        ["Taipei Standard Time"]            = "Taipei",
        ["SE Asia Standard Time"]           = "Bangkok",
        ["West Asia Standard Time"]         = "Islamabad",
        ["Pakistan Standard Time"]          = "Karachi",
        ["Sri Lanka Standard Time"]         = "Colombo",
        ["Bangladesh Standard Time"]        = "Dhaka",
        ["Myanmar Standard Time"]           = "Yangon",
        ["Afghanistan Standard Time"]       = "Kabul",
        ["Iran Standard Time"]              = "Tehran",
        ["Israel Standard Time"]            = "Jerusalem",
        ["Jordan Standard Time"]            = "Amman",
        ["Middle East Standard Time"]       = "Beirut",
        ["Egypt Standard Time"]             = "Cairo",
        ["Syria Standard Time"]             = "Damascus",

        // ── Oceania ──
        ["AUS Eastern Standard Time"]       = "Sydney",
        ["New Zealand Standard Time"]       = "Auckland",
        ["Fiji Standard Time"]              = "Fiji",
        ["Samoa Standard Time"]             = "Samoa",
        ["W. Australia Standard Time"]      = "Perth",
        ["Cen. Australia Standard Time"]    = "Adelaide",
        ["E. Australia Standard Time"]      = "Brisbane",
        ["Tasmania Standard Time"]          = "Hobart",

        // ── Africa ──
        ["South Africa Standard Time"]      = "Johannesburg",
        ["W. Central Africa Standard Time"] = "Lagos",
        ["E. Africa Standard Time"]         = "Nairobi",
        ["Morocco Standard Time"]           = "Casablanca",

        ["UTC"] = "UTC",
    };

    public static string Format(TimeZoneInfo tz)
    {
        var city = GetCityName(tz);
        var utc  = FormatOffset(tz.GetUtcOffset(DateTimeOffset.UtcNow));
        return $"{city} ({utc})";
    }

    public static string GetCityName(TimeZoneInfo tz)
    {
        if (KnownCities.TryGetValue(tz.Id, out var name)) return name;

        // Try extracting first city name from Windows display string:
        // e.g. "(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna" -> "Amsterdam"
        var d = tz.DisplayName;
        var closingParen = d.IndexOf(')');
        if (closingParen >= 0 && closingParen < d.Length - 1)
        {
            var rest = d[(closingParen + 1)..].Trim();
            var comma = rest.IndexOf(',');
            if (comma > 0)
                return rest[..comma].Trim();
            return rest;
        }

        return tz.StandardName;
    }

    public static string FormatOffset(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero) return "UTC";
        var sign = offset > TimeSpan.Zero ? "+" : "-";
        var h = Math.Abs(offset.Hours);
        var m = Math.Abs(offset.Minutes);
        return m > 0 ? $"UTC{sign}{h}:{m:D2}" : $"UTC{sign}{h}";
    }
}

/// <summary>Wraps a timezone with friendly display string for UI binding.</summary>
public class TimezoneItem
{
    public string Id { get; }
    public string Display { get; }

    public TimezoneItem(string id)
    {
        Id = id;
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            Display = TimezoneHelper.Format(tz);
        }
        catch
        {
            Display = id;
        }
    }

    public TimezoneItem(TimeZoneInfo tz)
    {
        Id = tz.Id;
        Display = TimezoneHelper.Format(tz);
    }

    public override string ToString() => Display;
}
