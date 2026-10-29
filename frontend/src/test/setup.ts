import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// jsdom no implementa la captura de punteros ni scrollIntoView, que usa el Select de Radix.
// Solo se simulan si no existen
if (!Element.prototype.hasPointerCapture) {
  Element.prototype.hasPointerCapture = () => false
}
if (!Element.prototype.releasePointerCapture) {
  Element.prototype.releasePointerCapture = () => {}
}
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {}
}

// jsdom tampoco tiene ResizeObserver, que usan los popper de Radix (Tooltip) al abrirse
if (!('ResizeObserver' in globalThis)) {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}

// Sin globals de Vitest, Testing Library no desmonta solo: se limpia tras cada test
afterEach(() => {
  cleanup()
})
