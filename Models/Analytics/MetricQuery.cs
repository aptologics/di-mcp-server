using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DI.MCP.Server.Models.Analytics;

#region Enums

/// <summary>
/// Predicate operator types for where clause operands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PredicateOperator
{
    TIME_PERIOD = 0,
    BETWEEN,
    GREATER_THAN,
    INCLUDES,
    EQUALS,
    TIME_SERIES,
    EXCLUDES
}

/// <summary>
/// Trend increment types for trendBy configuration.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Increment
{
    CONSECUTIVE = 0,
    WEEK_OVER_WEEK = 1,
    MONTH_OVER_MONTH = 2,
    QUARTER_OVER_QUARTER = 3,
    YEAR_OVER_YEAR = 4
}

/// <summary>
/// Group by options for metric queries.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MetricQueryGroupBy
{
    BASE = 0,
    PROVIDER = 1,
    PRACTICE = 2,
    PRACTICE_PROVIDER = 3,
    ONLY_PROVIDER = 4,
    ONLY_PRACTICE = 5,
    ONLY_PRACTICE_PROVIDER = 6
}

/// <summary>
/// Time interval granularity for date filters.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Interval
{
    DAY = 0,
    WEEK = 1,
    MONTH = 2,
    QUARTER = 3,
    YEAR = 4,
    RANGE = 5
}

/// <summary>
/// Relative time period for TIME_PERIOD operands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Period
{
    LAST = 0,
    CURRENT = 1,
    NEXT = 2,
    CUSTOM = 3
}

/// <summary>
/// Time direction for TIME_SERIES operands.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Projection
{
    BACKWARD = 0,
    FORWARD = 1
}

/// <summary>
/// Display group by options for query options.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisplayGroupBy
{
    REGION = 1,
    ENTERPRISE = 2,
    PROCEDURE_CATEGORY = 3,
    PRACTICE = 4,
    TITLE = 5
}

#endregion

#region Operands

/// <summary>
/// Date range filter using a named time period.
/// Two patterns:
///   1. Relative: interval (MONTH) + period (LAST)
///   2. Explicit: interval (RANGE) + startDate + endDate
/// </summary>
public class TimePeriodOperand
{
    [JsonPropertyName("operator")]
    public PredicateOperator Operator { get; set; } = PredicateOperator.TIME_PERIOD;

    [JsonPropertyName("interval")]
    public Interval Interval { get; set; }

    [JsonPropertyName("period")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Period? Period { get; set; }

    [JsonPropertyName("span")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Span { get; set; }

    [JsonPropertyName("startDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Inclusion / exclusion filter on a list of values.
/// </summary>
public class ValueOperand
{
    [JsonPropertyName("operator")]
    public PredicateOperator Operator { get; set; }

    [JsonPropertyName("value")]
    public List<string> Value { get; set; } = [];
}

/// <summary>
/// Date series filter using TIME_SERIES operator.
/// Returns multiple consecutive time periods anchored at startDate.
/// Used exclusively with POST /Analytics/Metrics/Query/Series.
/// </summary>
public class TimeSeriesOperand
{
    [JsonPropertyName("operator")]
    public PredicateOperator Operator { get; set; } = PredicateOperator.TIME_SERIES;

    /// <summary>Time granularity: DAY | WEEK | MONTH | QUARTER | YEAR</summary>
    [JsonPropertyName("interval")]
    public Interval Interval { get; set; }

    /// <summary>Number of periods to return (1–24)</summary>
    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    /// <summary>BACKWARD (historical, default) or FORWARD (future/scheduled)</summary>
    [JsonPropertyName("projection")]
    public Projection Projection { get; set; } = Projection.BACKWARD;

    /// <summary>Only supported value: CONSECUTIVE</summary>
    [JsonPropertyName("increment")]
    public Increment Increment { get; set; } = Increment.CONSECUTIVE;

    /// <summary>Anchor start date in YYYY-MM-DD format</summary>
    [JsonPropertyName("startDate")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime StartDate { get; set; }

    [JsonPropertyName("endDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Polymorphic JSON converter for WhereClause.Operand.
/// Discriminates on the "operator" field:
///   - TIME_PERIOD → TimePeriodOperand
///   - TIME_SERIES → TimeSeriesOperand
///   - INCLUDES / EXCLUDES / EQUALS → ValueOperand
/// </summary>
public class OperandConverter : JsonConverter<object>
{
    private static readonly HashSet<string> ValueOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "INCLUDES", "EXCLUDES", "EQUALS", "NOT_EQUALS"
    };

    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var op = root.TryGetProperty("operator", out var opProp)
            ? opProp.GetString() ?? ""
            : "";

        if (op.Equals("TIME_PERIOD", StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.Deserialize<TimePeriodOperand>(root.GetRawText(), options);

        if (op.Equals("TIME_SERIES", StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.Deserialize<TimeSeriesOperand>(root.GetRawText(), options);

        if (ValueOperators.Contains(op))
            return JsonSerializer.Deserialize<ValueOperand>(root.GetRawText(), options);

        throw new JsonException($"Unknown operand operator: '{op}'");
    }

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

#endregion

#region Query Components

/// <summary>
/// A single filter condition in the query where array (BasePredicate).
/// Common propertyIds: "dateKpi", "practiceId", "providerId", "staffId"
/// </summary>
public class WhereClause
{
    [JsonPropertyName("propertyId")]
    public string PropertyId { get; set; } = "";

    [JsonPropertyName("operand")]
    [JsonConverter(typeof(OperandConverter))]
    public object Operand { get; set; } = null!;
}

/// <summary>
/// Trend configuration (SimpleTrendByData).
/// Note: The API's SimpleTrendByData only has iterations and increment - no interval.
/// </summary>
public class TrendBy
{
    /// <summary>Number of additional historical periods (1–12)</summary>
    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    /// <summary>Trend increment type</summary>
    [JsonPropertyName("increment")]
    public Increment Increment { get; set; } = Increment.CONSECUTIVE;
}

/// <summary>
/// Query options for metric results (SimpleSettingsViewData).
/// </summary>
public class QueryOptions
{
    [JsonPropertyName("includeGoals")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeGoals { get; set; }

    [JsonPropertyName("displayGroupBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DisplayGroupBy? DisplayGroupBy { get; set; }

    [JsonPropertyName("includeInputPeriod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeInputPeriod { get; set; }

    [JsonPropertyName("includeTimers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeTimers { get; set; }

    [JsonPropertyName("includeNullRows")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeNullRows { get; set; }

    [JsonPropertyName("includeVerboseDebug")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeVerboseDebug { get; set; }

    [JsonPropertyName("includeForceVerboseDebug")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeForceVerboseDebug { get; set; }

    [JsonPropertyName("includeSubtotalsBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IncludeSubtotalsBy { get; set; }

    [JsonPropertyName("includeCacheId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeCacheId { get; set; }

    [JsonPropertyName("includeBenchmarks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeBenchmarks { get; set; }

    [JsonPropertyName("includeCurrentPeriodInTrend")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IncludeCurrentPeriodInTrend { get; set; }

    [JsonPropertyName("practiceProductionCalculationMethod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PracticeProductionCalculationMethod { get; set; }
}

#endregion

#region MetricQuery

/// <summary>
/// Full payload for POST /Analytics/Metrics/Query (AnalyticsMetricInputQuery).
/// 
/// IMPORTANT: The "from" field is a C# reserved keyword.
/// We use [JsonPropertyName("from")] to serialize correctly.
/// </summary>
public class MetricQuery
{
    [JsonPropertyName("select")]
    public List<string> Select { get; set; } = [];

    /// <summary>
    /// Subject: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF
    /// NOTE: "from" is a C# reserved word — property name is "From" but serializes as "from"
    /// </summary>
    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("where")]
    public List<WhereClause> Where { get; set; } = [];

    [JsonPropertyName("trendBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrendBy? TrendBy { get; set; }

    [JsonPropertyName("options")]
    public QueryOptions Options { get; set; } = new();

    [JsonPropertyName("groupBy")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MetricQueryGroupBy? GroupBy { get; set; }

    [JsonPropertyName("rootPracticeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? RootPracticeId { get; set; }

    [JsonPropertyName("customDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime? CustomDate { get; set; }

    [JsonPropertyName("clientCurrentDate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTime? ClientCurrentDate { get; set; }

    [JsonPropertyName("grantId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GrantId { get; set; }

    [JsonPropertyName("providerPulseType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProviderPulseType { get; set; }
}

#endregion

#region Converters

/// <summary>
/// Flexible DateTime converter that handles multiple date formats from AI tool calls.
/// Accepts: yyyy-MM-dd, MM-dd-yyyy, MM/dd/yyyy, and other common formats.
/// Always serializes as yyyy-MM-dd for downstream API compatibility.
/// </summary>
public class FlexibleDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "MM-dd-yyyy",
        "MM/dd/yyyy",
        "M-d-yyyy",
        "M/d/yyyy"
    ];

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected a date string or null.");

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        throw new JsonException($"Unable to parse '{value}' as a DateTime. Expected format: yyyy-MM-dd.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
    }
}

#endregion
