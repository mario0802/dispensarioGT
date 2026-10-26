namespace BuildingBlocks.Domain;

// Se lanza cuando un aggregate rompe una regla de negocio; CustomExceptionHandler la traduce a 400
public class DomainException(string message) : Exception(message);
