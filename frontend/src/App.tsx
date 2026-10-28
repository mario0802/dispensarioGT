import { ModeToggle } from '@/components/mode-toggle'
import { Button } from '@/components/ui/button'

// Página estática inicial: todavía no llama a la API
function App() {
  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b px-6 py-4">
        <h1 className="text-xl font-semibold">Dispensario Médico</h1>
        <ModeToggle />
      </header>
      <main className="flex flex-1 flex-col items-center justify-center gap-4 p-6">
        <p className="text-muted-foreground">Frontend en construcción.</p>
        <Button>Comenzar</Button>
      </main>
    </div>
  )
}

export default App
