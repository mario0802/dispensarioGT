// Primer carácter completo (Array.from no parte los caracteres fuera del BMP), en mayúsculas
function firstLetter(value: string): string {
  const [first = ''] = Array.from(value.trim())
  return first.toLocaleUpperCase('es')
}

// Iniciales del avatar: primera letra del nombre y del apellido; sin ninguna, '?'
export function getInitials(firstName: string, lastName: string): string {
  const initials = firstLetter(firstName) + firstLetter(lastName)
  return initials === '' ? '?' : initials
}
