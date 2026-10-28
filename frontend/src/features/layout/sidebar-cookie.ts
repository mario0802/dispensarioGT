// Cookie que escribe el SidebarProvider de shadcn al plegar o desplegar
export const SIDEBAR_COOKIE_NAME = 'sidebar_state'

// Estado inicial del sidebar: plegado solo si la cookie dice false; sin cookie o con un valor raro, desplegado
export function readSidebarOpen(): boolean {
  const value = document.cookie
    .split(';')
    .map((cookie) => cookie.trim())
    .find((cookie) => cookie.startsWith(`${SIDEBAR_COOKIE_NAME}=`))
    ?.slice(SIDEBAR_COOKIE_NAME.length + 1)

  return value !== 'false'
}
