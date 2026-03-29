var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("AiTeamDb");

builder.AddProject<Projects.AiTeam_Bot>("aiteam-bot")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.Build().Run();
