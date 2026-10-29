import { ArrowLeft } from 'lucide-react'
import { Link, useParams } from 'react-router'

import { Button } from '@/components/ui/button'

// Página /consultations/:consultationId: provisional, el siguiente spec muestra el detalle
export function ConsultationPage() {
  const { consultationId } = useParams()

  return (
    <div
      className="flex flex-col items-start gap-3 p-6"
      data-consultation-id={consultationId}
    >
      <h2 className="text-lg font-semibold">Consulta</h2>
      <p className="text-muted-foreground">
        El detalle de la consulta estará disponible próximamente.
      </p>
      <Button asChild variant="outline">
        <Link to="/appointments">
          <ArrowLeft />
          Volver a citas
        </Link>
      </Button>
    </div>
  )
}
