/// <reference types="vite/client" />

// Sin índice libre en import.meta.env: un nombre de variable mal escrito no compila
interface ViteTypeOptions {
  strictImportMetaEnv: unknown
}

// Variables de entorno que usa la app (prefijo VITE_, se incrustan en el bundle)
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
