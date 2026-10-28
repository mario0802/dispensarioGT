import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from 'react'

import { type ApiError, toApiError } from '@/lib/api/api-error'

export type ApiStatus = 'idle' | 'loading' | 'success' | 'error'

export type ApiResult<TData> =
  { data: TData; error: null } | { data: null; error: ApiError }

export interface UseApiResult<TArgs extends unknown[], TData> {
  data: TData | null
  error: ApiError | null
  status: ApiStatus
  isLoading: boolean
  execute: (...args: TArgs) => Promise<ApiResult<TData>>
  reset: () => void
  setData: (
    value: TData | null | ((previous: TData | null) => TData | null),
  ) => void
}

interface ApiState<TData> {
  data: TData | null
  error: ApiError | null
  status: ApiStatus
}

const initialState: ApiState<never> = {
  data: null,
  error: null,
  status: 'idle',
}

// Forma única de llamar a la API desde un componente: execute nunca rechaza y solo cuenta la última llamada
export function useApi<TArgs extends unknown[], TData>(
  fn: (...args: TArgs) => Promise<TData>,
): UseApiResult<TArgs, TData> {
  const [state, setState] = useState<ApiState<TData>>(initialState)

  // execute usa siempre la fn del último render sin cambiar de identidad
  const fnRef = useRef(fn)
  useLayoutEffect(() => {
    fnRef.current = fn
  })

  // Cada execute, reset y desmontaje incrementa el contador: solo la llamada vigente toca el estado
  const callIdRef = useRef(0)
  useEffect(
    () => () => {
      callIdRef.current++
    },
    [],
  )

  const execute = useCallback(
    async (...args: TArgs): Promise<ApiResult<TData>> => {
      const callId = ++callIdRef.current
      const isCurrent = () => callId === callIdRef.current

      // Mientras carga se conserva data: al refrescar, la pantalla no se vacía
      setState((previous) => ({ ...previous, error: null, status: 'loading' }))
      try {
        const data = await fnRef.current(...args)
        if (isCurrent()) {
          setState({ data, error: null, status: 'success' })
        }
        return { data, error: null }
      } catch (reason) {
        const error = toApiError(reason)
        if (isCurrent()) {
          setState((previous) => ({ ...previous, error, status: 'error' }))
        }
        return { data: null, error }
      }
    },
    [],
  )

  const reset = useCallback(() => {
    callIdRef.current++
    setState(initialState)
  }, [])

  const setData = useCallback<UseApiResult<TArgs, TData>['setData']>(
    (value) => {
      setState((previous) => ({
        ...previous,
        data:
          typeof value === 'function'
            ? (value as (previous: TData | null) => TData | null)(previous.data)
            : value,
      }))
    },
    [],
  )

  return {
    ...state,
    isLoading: state.status === 'loading',
    execute,
    reset,
    setData,
  }
}
