// Forma de los listados paginados de la API
export interface PaginatedResult<T> {
  pageIndex: number
  pageSize: number
  count: number
  data: T[]
}
