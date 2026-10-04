/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Comma-separated features served by the mock client: search, compare, analytics. */
  readonly VITE_MOCKS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
