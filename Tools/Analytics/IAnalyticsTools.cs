using DIMCPServer.Models.Analytics;
using DIMCPServer.Prompts.Analytics;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace DIMCPServer.Tools.Analytics
{
    /// <summary>
    /// Defines methods for retrieving metric definitions, querying metric results, obtaining metric series data, and
    /// accessing industry benchmarks for analytics purposes.
    /// </summary>
    /// <remarks>Implementations of this interface provide analytics tools for querying and analyzing metrics
    /// across various subjects such as practices, providers, procedures, referral sources, insurance carriers, and
    /// staff. Methods support flexible filtering, grouping, and trend analysis options to accommodate a wide range of
    /// reporting and benchmarking scenarios.</remarks>
    public interface IAnalyticsTools
    {
        [McpServerTool(Name = "get_metric_definitions"), Description(AnalyticsToolsDescriptionProvider.GetMetricDefinitions)]
        Task<string> GetMetricDefinitions(
            [Description("The subject to get metrics for: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF")]
            string? subject);

        [McpServerTool(Name = "query_metric_result"), Description(AnalyticsToolsDescriptionProvider.QueryMetricResult)]
        Task<string> QueryMetricResult(
            [Description("List of mnemonicKeys to retrieve (e.g. [\"grossProduction\", \"collections\", \"practiceId\", \"practiceName\"])")]
            List<string> select,

            [Description("Subject: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF")]
            string from,

            [Description("Filter conditions array. Must include a dateKpi filter with operator TIME_PERIOD. Example: [{\"propertyId\":\"dateKpi\",\"operand\":{\"operator\":\"TIME_PERIOD\",\"interval\":\"MONTH\",\"period\":\"LAST\"}},{\"propertyId\":\"practiceId\",\"operand\":{\"operator\":\"INCLUDES\",\"value\":[\"<guid>\"]}}]")]
            List<WhereClause> where,

            [Description("GUID of the root/parent practice for the account")]
            string rootPracticeId,

            [Description("Trend configuration: { iterations: 1-12, increment: \"CONSECUTIVE\" | \"MONTH_OVER_MONTH\" | etc. }")]
            TrendBy? trendBy = null,

            [Description("Query options: { includeGoals, includeNullRows, includeCacheId, includeCurrentPeriodInTrend }")]
            QueryOptions? options = null,

            [Description("Group results by: BASE | PROVIDER | PRACTICE | PRACTICE_PROVIDER | ONLY_PROVIDER | ONLY_PRACTICE | ONLY_PRACTICE_PROVIDER")]
            string? groupBy = null,

            [Description("Today's date (MM-DD-YYYY or YYYY-MM-DD). Defaults to server's current date.")]
            string? clientCurrentDate = null,

            [Description("Grant ID")]
            int? grantId = null);

        [McpServerTool(Name = "query_metric_series_result"), Description(AnalyticsToolsDescriptionProvider.QueryMetricSeriesResult)]
        Task<string> QueryMetricSeriesResult(
            [Description("List of mnemonicKeys to retrieve. Always include: \"startDate\", \"endDate\", \"practiceId\", \"practiceName\" plus desired metrics (e.g. \"grossProduction\", \"collections\")")]
            List<string> select,

            [Description("Subject: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF")]
            string from,

            [Description("Filter conditions as JSON array. Must include a dateKpi filter with operator TIME_SERIES. Example: [{\"propertyId\":\"dateKpi\",\"operand\":{\"operator\":\"TIME_SERIES\",\"interval\":\"MONTH\",\"iterations\":3,\"projection\":\"BACKWARD\",\"increment\":\"CONSECUTIVE\",\"startDate\":\"2026-02-01\"}},{\"propertyId\":\"practiceId\",\"operand\":{\"operator\":\"INCLUDES\",\"value\":[\"<guid>\"]}}]")]
            List<WhereClause> where,

            [Description("GUID of the root/parent practice for the account")]
            string rootPracticeId,

            [Description("Trend configuration: { iterations: 1-12, increment: \"CONSECUTIVE\" | \"MONTH_OVER_MONTH\" | etc. }")]
            TrendBy? trendBy = null,

            [Description("Query options: { includeInputPeriod, includeNullRows, includeGoals, includeCacheId }")]
            QueryOptions? options = null,

            [Description("Group results by: BASE | PROVIDER | PRACTICE | PRACTICE_PROVIDER | ONLY_PROVIDER | ONLY_PRACTICE | ONLY_PRACTICE_PROVIDER")]
            string? groupBy = null,

            [Description("Today's date (MM-DD-YYYY or YYYY-MM-DD). Defaults to server's current date.")]
            string? clientCurrentDate = null,

            [Description("Grant ID")]
            int? grantId = null);

        [McpServerTool(Name = "get_industry_benchmarks"), Description(AnalyticsToolsDescriptionProvider.GetIndustryBenchmarks)]
        Task<string> GetIndustryBenchmarks(
            [Description("List of mnemonicKeys to retrieve (e.g. [\"grossProduction\", \"collections\", \"practiceId\", \"practiceName\"])")]
            List<string> select,

            [Description("Subject: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF")]
            string from,

            [Description("Filter conditions array. Must include a dateKpi filter with operator TIME_PERIOD. Example: [{\"propertyId\":\"dateKpi\",\"operand\":{\"operator\":\"TIME_PERIOD\",\"interval\":\"MONTH\",\"period\":\"LAST\"}},{\"propertyId\":\"practiceId\",\"operand\":{\"operator\":\"INCLUDES\",\"value\":[\"<guid>\"]}}]")]
            List<WhereClause> where,

            [Description("GUID of the root/parent practice for the account")]
            string rootPracticeId,

            [Description("Query options: { includeGoals, includeNullRows, includeCacheId }. Note: includeBenchmarks is auto-set.")]
            QueryOptions? options = null,

            [Description("Group results by: BASE | PROVIDER | PRACTICE | PRACTICE_PROVIDER | ONLY_PROVIDER | ONLY_PRACTICE | ONLY_PRACTICE_PROVIDER")]
            string? groupBy = null,

            [Description("Today's date (MM-DD-YYYY or YYYY-MM-DD). Defaults to server's current date.")]
            string? clientCurrentDate = null,

            [Description("Grant ID")]
            int? grantId = null);
    }
}
