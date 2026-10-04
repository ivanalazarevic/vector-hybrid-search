interface Faceted {
  category?: string;
  source?: string;
}

/**
 * Filter suggestions. The backend has no facets endpoint, so the category and source inputs
 * suggest the distinct values seen in the results currently on the page.
 */
export function collectFacets(results: readonly Faceted[]): { categories: string[]; sources: string[] } {
  const distinct = (values: (string | undefined)[]) =>
    [...new Set(values.filter((value): value is string => !!value))].sort((left, right) => left.localeCompare(right));

  return {
    categories: distinct(results.map((result) => result.category)),
    sources: distinct(results.map((result) => result.source)),
  };
}
