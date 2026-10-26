// Host mínimo: la configuración real (DI, Carter, OpenAPI, auth) llega en la fila 05
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();
