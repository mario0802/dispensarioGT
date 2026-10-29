import { Check, ChevronsUpDown, Loader2 } from 'lucide-react'
import { useEffect, useState } from 'react'

import { Button } from '@/components/ui/button'
import {
  Command,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/command'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { useApi } from '@/hooks/use-api'
import { useDebouncedValue } from '@/hooks/use-debounced-value'
import { cn } from '@/lib/utils'

const searchDebounceMs = 300

// Valor reservado del ítem "Todos…": los ids de la API son GUID y no chocan
const clearItemValue = '__clear__'

export interface SearchComboboxOption {
  id: string
  // Texto del botón y de la opción
  label: string
  // Segunda línea atenuada en la opción
  description?: string
}

interface SearchComboboxProps {
  value: SearchComboboxOption | null
  onChange: (option: SearchComboboxOption | null) => void
  // Llama a la API y mapea el resultado; el combobox la ejecuta con useApi
  search: (text: string) => Promise<SearchComboboxOption[]>
  placeholder: string
  searchPlaceholder: string
  emptyText: string
  ariaLabel: string
  // Filtros: muestra el ítem clearLabel, que deja el valor en null
  clearable?: boolean
  clearLabel?: string
  disabled?: boolean
  invalid?: boolean
  id?: string
}

// Combobox (Popover + Command) que busca en la API: el filtrado es del servidor, no de cmdk
export function SearchCombobox({
  value,
  onChange,
  search,
  placeholder,
  searchPlaceholder,
  emptyText,
  ariaLabel,
  clearable = false,
  clearLabel,
  disabled,
  invalid,
  id,
}: SearchComboboxProps) {
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const debouncedText = useDebouncedValue(text.trim(), searchDebounceMs)
  const { data, error, isLoading, execute } = useApi(search)

  // Al abrirse pide la primera página sin texto; al escribir, tras el debounce
  useEffect(() => {
    if (open) {
      void execute(debouncedText)
    }
  }, [open, debouncedText, execute])

  const handleOpenChange = (next: boolean) => {
    setOpen(next)
    if (!next) {
      setText('')
    }
  }

  const select = (option: SearchComboboxOption | null) => {
    onChange(option)
    handleOpenChange(false)
  }

  const options = data ?? []

  return (
    // modal: dentro de un Dialog, el popover recibe el foco y el scroll
    <Popover open={open} onOpenChange={handleOpenChange} modal>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-label={ariaLabel}
          aria-invalid={invalid || undefined}
          disabled={disabled}
          className="w-full justify-between font-normal"
        >
          <span className={cn('truncate', !value && 'text-muted-foreground')}>
            {value?.label ?? placeholder}
          </span>
          <ChevronsUpDown className="opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className="w-(--radix-popover-trigger-width) min-w-64 p-0"
        align="start"
      >
        <Command shouldFilter={false}>
          <CommandInput
            placeholder={searchPlaceholder}
            value={text}
            onValueChange={setText}
          />
          <CommandList>
            {isLoading && (
              <p className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                Buscando…
              </p>
            )}
            {!isLoading && error && (
              <p className="py-6 text-center text-sm text-destructive">
                No se pudo buscar. Inténtalo de nuevo.
              </p>
            )}
            {!isLoading && !error && data && options.length === 0 && (
              <p className="py-6 text-center text-sm text-muted-foreground">
                {emptyText}
              </p>
            )}
            {!isLoading && !error && (clearable || options.length > 0) && (
              <CommandGroup>
                {clearable && (
                  <CommandItem
                    value={clearItemValue}
                    onSelect={() => select(null)}
                  >
                    <Check
                      className={cn(value ? 'opacity-0' : 'opacity-100')}
                    />
                    {clearLabel}
                  </CommandItem>
                )}
                {options.map((option) => (
                  <CommandItem
                    key={option.id}
                    value={option.id}
                    onSelect={() => select(option)}
                  >
                    <Check
                      className={cn(
                        value?.id === option.id ? 'opacity-100' : 'opacity-0',
                      )}
                    />
                    <div className="flex min-w-0 flex-col">
                      <span className="truncate">{option.label}</span>
                      {option.description && (
                        <span className="truncate text-xs text-muted-foreground">
                          {option.description}
                        </span>
                      )}
                    </div>
                  </CommandItem>
                ))}
              </CommandGroup>
            )}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  )
}
