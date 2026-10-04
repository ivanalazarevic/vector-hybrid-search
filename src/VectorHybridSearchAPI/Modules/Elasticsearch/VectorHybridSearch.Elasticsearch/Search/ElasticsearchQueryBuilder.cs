using System.Globalization;
using VectorHybridSearch.Shared.Contracts.Search;

namespace VectorHybridSearch.Elasticsearch.Search;

public static class ElasticsearchQueryBuilder
{
    public static object BuildFilteredTextQuery(SearchRequest request)
    {
        var textQuery = BuildBoostedTextQuery(request.Query);

        var filters = BuildFilters(request.Filters);
        if (filters.Count == 0) {
            return textQuery;
        }

        return new {
            @bool = new {
                must = new object[] {
                    textQuery
                },
                filter = filters
            }
        };
    }

    public static object BuildBoostedTextQuery(string query)
    {
        var keywordQuery = new {
            multi_match = new {
                query,
                fields = new[] {
                    "chunkText^3",
                    "title^4",
                    "summary"
                },
                type = "best_fields"
            }
        };

        return new {
            @bool = new {
                should = new object[] {
                    keywordQuery,
                    new {
                        match_phrase = new Dictionary<string, object> {
                            ["title"] = new {
                                query,
                                boost = 8
                            }
                        }
                    },
                    new {
                        match_phrase = new Dictionary<string, object> {
                            ["chunkText"] = new {
                                query,
                                boost = 5
                            }
                        }
                    }
                },
                minimum_should_match = 1
            }
        };
    }

    public static IReadOnlyCollection<object> BuildFilters(SearchFilters? filters)
    {
        if (filters is null) {
            return [];
        }

        var elasticFilters = new List<object>();

        if (!string.IsNullOrWhiteSpace(filters.Category)) {
            elasticFilters.Add(new {
                term = new Dictionary<string, object> {
                    ["category"] = filters.Category
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(filters.Source)) {
            elasticFilters.Add(new {
                term = new Dictionary<string, object> {
                    ["source"] = filters.Source
                }
            });
        }

        if (filters.PublishedFrom is not null || filters.PublishedTo is not null) {
            var range = new Dictionary<string, string>();

            if (filters.PublishedFrom is not null) {
                range["gte"] = filters.PublishedFrom.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            if (filters.PublishedTo is not null) {
                range["lte"] = filters.PublishedTo.Value.ToString("O", CultureInfo.InvariantCulture);
            }

            elasticFilters.Add(new {
                range = new Dictionary<string, object> {
                    ["publishedAt"] = range
                }
            });
        }

        return elasticFilters;
    }

    // The html encoder escapes the chunk text so the only markup in a snippet is the <em> highlight tags.
    public static object BuildChunkTextHighlight()
    {
        return new {
            encoder = "html",
            fields = new Dictionary<string, object> {
                ["chunkText"] = new {
                    fragment_size = 220,
                    number_of_fragments = 1
                }
            }
        };
    }
}
