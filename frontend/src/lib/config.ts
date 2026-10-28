// Configuración de la app leída de las variables de entorno de Vite
export interface AppConfig {
  // URL base de la API, sin barra final
  apiBaseUrl: string
}

// Función pura para poder probar los casos de error sin tocar import.meta.env
export function readConfig(env: Partial<ImportMetaEnv>): AppConfig {
  const apiBaseUrl = env.VITE_API_BASE_URL?.trim().replace(/\/+$/, '')
  if (!apiBaseUrl) {
    throw new Error('VITE_API_BASE_URL is not configured.')
  }
  return { apiBaseUrl }
}

export const config: AppConfig = readConfig(import.meta.env)
