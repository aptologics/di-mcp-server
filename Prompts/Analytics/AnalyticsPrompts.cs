using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DI.MCP.Server.Prompts.Analytics;

/// <summary>
/// MCP Prompts for Analytics — user-controlled templates that guide the LLM
/// through structured analytics workflows using available tools and resources.
/// Discoverable via prompts/list, retrievable via prompts/get.
/// </summary>
[McpServerPromptType]
public sealed class AnalyticsPrompts
{
    [McpServerPrompt(Name = "explore-metrics"),
     Description("Discover available KPI metrics for a given subject. Guides the LLM to call get_metric_definitions and present results grouped by classification.")]
    public static IList<PromptMessage> ExploreMetrics(
        [Description("The analytics subject: PRACTICE | PROVIDER | PROCEDURE | REFERRAL_SOURCE | INSURANCE_CARRIER | STAFF")]
        string subject = "PRACTICE")
    {
        return
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Show me all available KPI metrics for the {subject} subject."
                }
            },
            new PromptMessage
            {
                Role = Role.Assistant,
                Content = new TextContentBlock
                {
                    Text = $"""
                        I'll retrieve the metric catalog for {subject} using `get_metric_definitions`.

                        Once I have the results, I will:
                        1. Group metrics by classification (PRODUCTION, OPERATIONS, APPOINTMENTS, MARKETING)
                        2. Present each group as a table with mnemonicKey, Label, Description, and Format
                        3. Highlight the mnemonicKey — it is required for querying actual data

                        Let me fetch the definitions now.
                        """
                }
            }
        ];
    }

    [McpServerPrompt(Name = "query-practice-snapshot"),
     Description("Query a single-period snapshot of practice metrics. Guides the LLM to build a query_metric_result payload for the specified metrics and time period.")]
    public static IList<PromptMessage> QueryPracticeSnapshot(
        [Description("Comma-separated metric names (e.g. 'production, collections, case acceptance')")]
        string metrics,

        [Description("Time period (e.g. 'last month', 'current quarter', 'January 2025')")]
        string timePeriod = "last month",

        [Description("Practice ID (GUID) to filter results")]
        string? practiceId = null)
    {
        var practiceFilter = string.IsNullOrEmpty(practiceId)
            ? "across all practices"
            : $"for practice {practiceId}";

        return
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Show me {metrics} for {timePeriod} {practiceFilter}."
                }
            },
            new PromptMessage
            {
                Role = Role.Assistant,
                Content = new TextContentBlock
                {
                    Text = $"""
                        I'll query the practice metrics using `query_metric_result`.

                        Steps:
                        1. Map "{metrics}" to the correct mnemonicKeys
                        2. Build the dateKpi filter for "{timePeriod}"
                        3. Include practiceId/practiceName identifiers in select
                        4. Format results with proper CURRENCY/PERCENT/NUMBER formatting
                        5. Present data in a table followed by key insights

                        Let me build and execute the query now.
                        """
                }
            }
        ];
    }

    [McpServerPrompt(Name = "analyze-trends"),
     Description("Analyze metric trends over multiple periods. Guides the LLM to use query_metric_series_result for period-by-period breakdowns with trend indicators.")]
    public static IList<PromptMessage> AnalyzeTrends(
        [Description("Comma-separated metric names (e.g. 'production, collections')")]
        string metrics,

        [Description("Number of periods to analyze (e.g. '6')")]
        int periods = 6,

        [Description("Period interval: MONTH | QUARTER | YEAR")]
        string interval = "MONTH",

        [Description("Practice ID (GUID) to filter results")]
        string? practiceId = null)
    {
        var practiceFilter = string.IsNullOrEmpty(practiceId)
            ? "across all practices"
            : $"for practice {practiceId}";

        return
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Show me {metrics} trends for the last {periods} {interval.ToLower()}s {practiceFilter}."
                }
            },
            new PromptMessage
            {
                Role = Role.Assistant,
                Content = new TextContentBlock
                {
                    Text = $"""
                        I'll retrieve period-by-period data using `query_metric_series_result`.

                        Steps:
                        1. Map "{metrics}" to the correct mnemonicKeys
                        2. Build a TIME_SERIES dateKpi filter with interval={interval}, iterations={periods}, projection=BACKWARD
                        3. Include startDate, endDate, practiceId, practiceName in select
                        4. Present a period-over-period table (columns = periods, rows = practices)
                        5. Add trend indicators: 📈 improving / 🔻 worsening
                        6. Summarize with 2-4 insight bullet points

                        Let me build and execute the query now.
                        """
                }
            }
        ];
    }

    [McpServerPrompt(Name = "compare-providers"),
     Description("Compare metrics across providers within a practice. Guides the LLM to query by PROVIDER subject and present a comparative view.")]
    public static IList<PromptMessage> CompareProviders(
        [Description("Comma-separated metric names (e.g. 'production, collections, case acceptance')")]
        string metrics,

        [Description("Time period (e.g. 'last month', 'current quarter')")]
        string timePeriod = "last month",

        [Description("Root practice ID (GUID)")]
        string? rootPracticeId = null)
    {
        return
        [
            new PromptMessage
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Compare {metrics} across all providers for {timePeriod}."
                }
            },
            new PromptMessage
            {
                Role = Role.Assistant,
                Content = new TextContentBlock
                {
                    Text = $"""
                        I'll compare provider performance using `query_metric_result` with from=PROVIDER.

                        Steps:
                        1. Map "{metrics}" to mnemonicKeys
                        2. Set from=PROVIDER and include providerId/providerName in select
                        3. Build dateKpi filter for "{timePeriod}"
                        4. Present a comparison table (rows = providers, columns = metrics)
                        5. Highlight top and bottom performers
                        6. Flag providers below benchmark thresholds

                        Let me build and execute the query now.
                        """
                }
            }
        ];
    }
}
