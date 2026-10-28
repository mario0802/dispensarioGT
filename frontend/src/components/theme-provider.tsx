import { createContext, useContext, useEffect, useState } from 'react'

// Patrón oficial de shadcn/ui para el modo oscuro en Vite
export type Theme = 'light' | 'dark' | 'system'

const themes: readonly Theme[] = ['light', 'dark', 'system']

interface ThemeProviderProps {
  children: React.ReactNode
  defaultTheme?: Theme
  storageKey?: string
}

interface ThemeProviderState {
  theme: Theme
  setTheme: (theme: Theme) => void
}

const ThemeProviderContext = createContext<ThemeProviderState | undefined>(
  undefined,
)

// Un valor guardado que no es un tema válido se trata como el tema por defecto
function readStoredTheme(storageKey: string, defaultTheme: Theme): Theme {
  const stored = localStorage.getItem(storageKey)
  return themes.includes(stored as Theme) ? (stored as Theme) : defaultTheme
}

export function ThemeProvider({
  children,
  defaultTheme = 'system',
  storageKey = 'dispensario-theme',
}: ThemeProviderProps) {
  const [theme, setThemeState] = useState<Theme>(() =>
    readStoredTheme(storageKey, defaultTheme),
  )

  useEffect(() => {
    const root = window.document.documentElement
    root.classList.remove('light', 'dark')

    // Con 'system' se resuelve con la preferencia del sistema operativo
    const resolved =
      theme === 'system'
        ? window.matchMedia('(prefers-color-scheme: dark)').matches
          ? 'dark'
          : 'light'
        : theme

    root.classList.add(resolved)
  }, [theme])

  const setTheme = (next: Theme) => {
    localStorage.setItem(storageKey, next)
    setThemeState(next)
  }

  return (
    <ThemeProviderContext.Provider value={{ theme, setTheme }}>
      {children}
    </ThemeProviderContext.Provider>
  )
}

// El hook vive junto al provider, como en el patrón de shadcn/ui
// eslint-disable-next-line react-refresh/only-export-components
export function useTheme(): ThemeProviderState {
  const context = useContext(ThemeProviderContext)
  if (context === undefined) {
    throw new Error('useTheme must be used within a ThemeProvider.')
  }
  return context
}
