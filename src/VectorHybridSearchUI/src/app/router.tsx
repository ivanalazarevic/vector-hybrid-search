import { createBrowserRouter } from "react-router-dom";
import { EmptyState } from "../components/EmptyState";
import { ComparePage } from "../features/compare/ComparePage";
import { SearchPage } from "../features/search/SearchPage";
import { Layout } from "./Layout";

export const router = createBrowserRouter(
  [
    {
      path: "/",
      element: <Layout />,
      // Shown only when the app is opened directly on a lazily loaded page, until its code arrives.
      hydrateFallbackElement: <></>,
      children: [
        { index: true, element: <SearchPage /> },
        { path: "compare", element: <ComparePage /> },
        {
          // Loaded on demand: this page brings the chart library, which the other two do not need.
          path: "analytics",
          lazy: async () => ({ Component: (await import("../features/analytics/AnalyticsPage")).AnalyticsPage }),
        },
        { path: "*", element: <EmptyState title="This page does not exist">Use the navigation above.</EmptyState> },
      ],
    },
  ],
  {
    future: {
      v7_relativeSplatPath: true,
      v7_fetcherPersist: true,
      v7_normalizeFormMethod: true,
      v7_partialHydration: true,
      v7_skipActionErrorRevalidation: true,
    },
  },
);
