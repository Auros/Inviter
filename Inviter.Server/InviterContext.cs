using Inviter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Inviter.Server;

public class InviterContext : DbContext
{
    private readonly ILogger _logger;

    public DbSet<User> Users => Set<User>();

    public InviterContext(ILogger<InviterContext> logger, DbContextOptions<InviterContext> options) : base(options)
    {
        _logger = logger;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.LogTo(Log, LogLevel.Information, DbContextLoggerOptions.SingleLine);
    }

    private void Log(string command)
    {
        if (command.StartsWith("Executed DbCommand"))
        {
            int queryTime = int.Parse(Between(command, '(', 'm').Replace(",", string.Empty));
            string query = command[(command.LastIndexOf("]") + 1)..];
            _logger.LogInformation("[{Time}ms] | {Query}", queryTime, query);
        }
    }

    private static string Between(string input, char start, char end)
    {
        int iStart = input.IndexOf(start) + 1;
        int iEnd = input.IndexOf(end, iStart);
        return input[iStart..iEnd];
    }
}