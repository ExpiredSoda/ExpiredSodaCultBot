using CultBot.Configuration;
using CultBot.Data;
using CultBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace CultBot.Tests;

public sealed class InitiationWorkflowTests
{
    [Theory]
    [InlineData("announcement")]
    [InlineData("holding cleanup")]
    [InlineData("ritual cleanup")]
    public async Task Completion_is_persisted_before_optional_operations_even_when_they_fail(string failure)
    {
        var f = new Fixture();
        var session = await f.SeedAsync();
        f.Actions.FailOperation = failure;
        f.Actions.BeforeOptional = async () => Assert.Equal(InitiationSessionStatus.Completed, (await f.ReadAsync(session.Id))!.Status);

        Assert.Equal(InitiationOutcome.Completed, await f.CompleteAsync());
        var saved = await f.ReadAsync(session.Id);
        Assert.Equal(InitiationSessionStatus.Completed, saved!.Status);
        Assert.Equal("SilentWitness", saved.ChosenRole);
        Assert.NotNull(saved.CompletedTimeUtc);
        Assert.Equal(1, f.Actions.Announcements);
        Assert.Equal(1, f.Actions.Deletions);
    }

    [Fact]
    public async Task Role_failure_keeps_session_and_ritual_pending()
    {
        var f = new Fixture();
        var session = await f.SeedAsync();
        f.Actions.FailOperation = "role";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.CompleteAsync());
        Assert.Equal(InitiationSessionStatus.Pending, (await f.ReadAsync(session.Id))!.Status);
        Assert.Equal(0, f.Actions.Deletions);
        Assert.Equal(0, f.Actions.HoldingRemovals);
    }

    [Fact]
    public async Task Database_failure_preserves_retry_and_existing_role_is_not_granted_twice()
    {
        var f = new Fixture();
        var session = await f.SeedAsync();
        f.Fault.FailNextSave();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.CompleteAsync());
        Assert.Equal(InitiationSessionStatus.Pending, (await f.ReadAsync(session.Id))!.Status);
        Assert.Equal("SilentWitness", f.Actions.Member!.PathRole);
        Assert.Equal(0, f.Actions.Deletions);
        Assert.Equal(0, f.Actions.HoldingRemovals);

        Assert.Equal(InitiationOutcome.Completed, await f.CompleteAsync("NeonDisciple"));
        Assert.Equal("SilentWitness", (await f.ReadAsync(session.Id))!.ChosenRole);
        Assert.Equal(1, f.Actions.RoleGrants);
        Assert.Equal(InitiationOutcome.Skipped, await f.CompleteAsync());
    }

    [Fact]
    public async Task Concurrent_choices_and_repeated_clicks_grant_only_one_path()
    {
        var f = new Fixture();
        await f.SeedAsync();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => f.CompleteAsync(i % 2 == 0 ? "SilentWitness" : "NeonDisciple")));
        Assert.Single(outcomes, result => result == InitiationOutcome.Completed);
        Assert.Equal(1, f.Actions.RoleGrants);
        Assert.Equal(1, f.Actions.Announcements);
    }

    [Fact]
    public async Task Another_member_cannot_complete_someone_elses_ritual()
    {
        var f = new Fixture();
        await f.SeedAsync();
        Assert.Equal(InitiationOutcome.Skipped, await f.Workflow.CompleteAsync(Fixture.Guild, Fixture.User + 1, Fixture.Message, "SilentWitness", f.Actions));
        Assert.Equal(0, f.Actions.Fetches);
        Assert.Equal(0, f.Actions.RoleGrants);
    }

    [Fact]
    public async Task Old_membership_button_cannot_grant_a_role_after_rejoin()
    {
        var f = new Fixture();
        await f.SeedAsync();
        f.Actions.Member = f.Actions.Member! with { JoinedAtUtc = f.Now.AddHours(-1) };
        Assert.Equal(InitiationOutcome.Skipped, await f.CompleteAsync());
        Assert.Equal(0, f.Actions.RoleGrants);
    }

    [Fact]
    public async Task Expiration_waits_for_in_progress_completion_then_reloads_state()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Actions.BeforeGrant = async () => { entered.SetResult(); await release.Task; };
        var completion = f.CompleteAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var expiration = f.Workflow.ProcessExpirationAsync(candidate, f.Actions);
        Assert.False(expiration.IsCompleted);
        release.SetResult();
        Assert.Equal(InitiationOutcome.Completed, await completion);
        Assert.Equal(InitiationOutcome.Skipped, await expiration);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Fact]
    public async Task Stale_expiration_candidate_does_not_kick_a_completed_session()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        await f.Sessions.MarkSessionCompletedAsync(candidate.Id, "SilentWitness");
        Assert.Equal(InitiationOutcome.Skipped, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Fetches);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Fresh_path_role_at_either_expiration_check_prevents_kick(int fetch)
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.OnFetch = n => { if (n == fetch) f.Actions.Member = f.Actions.Member! with { PathRole = "NeonDisciple" }; return Task.CompletedTask; };
        Assert.Equal(InitiationOutcome.Completed, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(InitiationSessionStatus.Completed, (await f.ReadAsync(candidate.Id))!.Status);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Fact]
    public async Task Session_change_during_final_role_fetch_prevents_kick()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.OnFetch = async n => { if (n == 2) await f.Sessions.MarkSessionCompletedAsync(candidate.Id, "SilentWitness"); };
        Assert.Equal(InitiationOutcome.Skipped, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Theory]
    [InlineData("left")]
    [InlineData("bot")]
    [InlineData("protected")]
    [InlineData("no holding role")]
    [InlineData("rejoin")]
    [InlineData("legacy recovery")]
    [InlineData("unknown join")]
    public async Task Ineligible_members_are_retired_without_a_kick(string reason)
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.Member = reason switch
        {
            "left" => null,
            "bot" => f.Actions.Member! with { IsBot = true },
            "protected" => f.Actions.Member! with { IsProtected = true },
            "no holding role" => f.Actions.Member! with { HasHoldingRole = false },
            "rejoin" => f.Actions.Member! with { JoinedAtUtc = f.Now.AddHours(-1) },
            "legacy recovery" => f.Actions.Member! with { JoinedAtUtc = f.Now.AddDays(-30) },
            _ => f.Actions.Member! with { JoinedAtUtc = null }
        };
        Assert.Equal(InitiationOutcome.Expired, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Kicks);
        Assert.Equal(0, f.Actions.Deletions);
        Assert.Equal(InitiationSessionStatus.Expired, (await f.ReadAsync(candidate.Id))!.Status);
    }

    [Fact]
    public async Task Unkickable_hierarchy_keeps_pending_session_without_attempting_kick()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.Member = f.Actions.Member! with { CanKick = false };
        Assert.Equal(InitiationOutcome.Skipped, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Kicks);
        Assert.Equal(InitiationSessionStatus.Pending, (await f.ReadAsync(candidate.Id))!.Status);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Failed_authoritative_member_fetch_never_kicks(int fetch)
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.OnFetch = n => n == fetch ? Task.FromException(new InvalidOperationException("Simulated REST failure")) : Task.CompletedTask;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Kicks);
        Assert.Equal(InitiationSessionStatus.Pending, (await f.ReadAsync(candidate.Id))!.Status);
    }

    [Fact]
    public async Task Undelivered_reminder_does_not_start_grace_period_or_allow_kick()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync(reminded: false);
        f.Actions.ReminderDelivered = false;
        for (var i = 0; i < 3; i++)
            Assert.Equal(InitiationOutcome.Skipped, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Null((await f.ReadAsync(candidate.Id))!.ReminderSentAt);
        Assert.Equal(0, f.Actions.Kicks);
        Assert.Equal(3, f.Actions.Reminders);
    }

    [Fact]
    public async Task Concurrent_delivered_reminders_are_recorded_once_and_grace_is_respected()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync(reminded: false);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Workflow.ProcessExpirationAsync(candidate, f.Actions)));
        Assert.Single(outcomes, result => result == InitiationOutcome.ReminderSent);
        Assert.NotNull((await f.ReadAsync(candidate.Id))!.ReminderSentAt);
        Assert.Equal(1, f.Actions.Reminders);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Fact]
    public async Task Session_before_timeout_is_not_reminded_or_kicked()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync(joinTime: f.Now.AddHours(-1), reminded: false);
        Assert.Equal(InitiationOutcome.Skipped, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Reminders);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Theory]
    [InlineData("kick")]
    [InlineData("ritual cleanup")]
    [InlineData("expiration announcement")]
    public async Task Kick_failure_preserves_retry_and_post_kick_failures_preserve_expired_state(string failure)
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.FailOperation = failure;
        f.Actions.BeforeOptional = async () => Assert.Equal(InitiationSessionStatus.Expired, (await f.ReadAsync(candidate.Id))!.Status);
        if (failure == "kick")
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
            Assert.Equal(InitiationSessionStatus.Pending, (await f.ReadAsync(candidate.Id))!.Status);
            Assert.Equal(0, f.Actions.Deletions);
            Assert.Equal(0, f.Actions.ExpirationAnnouncements);
        }
        else
        {
            Assert.Equal(InitiationOutcome.Kicked, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
            Assert.Equal(InitiationSessionStatus.Expired, (await f.ReadAsync(candidate.Id))!.Status);
        }
    }

    [Fact]
    public async Task Repeated_concurrent_expiration_kicks_at_most_once()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => f.Workflow.ProcessExpirationAsync(candidate, f.Actions)));
        Assert.Single(outcomes, result => result == InitiationOutcome.Kicked);
        Assert.Equal(1, f.Actions.Kicks);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("recover")]
    [InlineData("expire")]
    public async Task Unsafe_configuration_disables_all_initiation_actions(string operation)
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.ConfigurationSafe = false;
        var outcome = operation switch
        {
            "complete" => await f.CompleteAsync(),
            "recover" => await f.RecoverAsync(),
            _ => await f.Workflow.ProcessExpirationAsync(candidate, f.Actions)
        };
        Assert.Equal(InitiationOutcome.ConfigurationDisabled, outcome);
        Assert.Equal(0, f.Actions.Fetches);
        Assert.Equal(0, f.Actions.RoleGrants);
        Assert.Equal(0, f.Actions.Rituals);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Fact]
    public async Task Configuration_change_at_final_check_prevents_kick()
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync();
        f.Actions.OnFetch = n => { if (n == 2) f.Actions.ConfigurationSafe = false; return Task.CompletedTask; };
        Assert.Equal(InitiationOutcome.ConfigurationDisabled, await f.Workflow.ProcessExpirationAsync(candidate, f.Actions));
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("recover")]
    [InlineData("expire")]
    public async Task Fresh_role_configuration_failure_prevents_member_changes(string operation)
    {
        var f = new Fixture();
        var candidate = await f.SeedAsync(reminded: false);
        f.Actions.OnFetch = _ => { f.Actions.ConfigurationSafe = false; return Task.CompletedTask; };
        var outcome = operation switch
        {
            "complete" => await f.CompleteAsync(),
            "recover" => await f.RecoverAsync(),
            _ => await f.Workflow.ProcessExpirationAsync(candidate, f.Actions)
        };
        Assert.Equal(InitiationOutcome.ConfigurationDisabled, outcome);
        Assert.Equal(0, f.Actions.RoleGrants);
        Assert.Equal(0, f.Actions.HoldingGrants);
        Assert.Equal(0, f.Actions.Reminders);
        Assert.Equal(0, f.Actions.Kicks);
    }

    [Theory]
    [InlineData("old")]
    [InlineData("unknown")]
    [InlineData("future")]
    [InlineData("bot")]
    [InlineData("protected")]
    [InlineData("path")]
    public async Task Recovery_only_enrolls_intended_newcomers(string reason)
    {
        var f = new Fixture();
        f.Actions.Member = reason switch
        {
            "old" => f.Actions.Member! with { JoinedAtUtc = f.Now.AddDays(-8) },
            "unknown" => f.Actions.Member! with { JoinedAtUtc = null },
            "future" => f.Actions.Member! with { JoinedAtUtc = f.Now.AddDays(1) },
            "bot" => f.Actions.Member! with { IsBot = true },
            "protected" => f.Actions.Member! with { IsProtected = true },
            _ => f.Actions.Member! with { PathRole = "SilentWitness" }
        };
        Assert.Equal(InitiationOutcome.Skipped, await f.RecoverAsync());
        Assert.Equal(0, f.Actions.HoldingGrants);
        Assert.Equal(0, f.Actions.Rituals);
        Assert.Null(await f.Sessions.GetLatestSessionAsync(Fixture.User, Fixture.Guild));
    }

    [Fact]
    public async Task Repeated_concurrent_recovery_creates_one_session_and_one_ritual()
    {
        var f = new Fixture();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => f.RecoverAsync()));
        Assert.Single(outcomes, result => result == InitiationOutcome.Created);
        Assert.Equal(1, f.Actions.HoldingGrants);
        Assert.Equal(1, f.Actions.Rituals);
        await using var context = f.Factory.CreateDbContext();
        Assert.Single(await context.InitiationSessions.ToListAsync());
    }

    [Theory]
    [InlineData(InitiationSessionStatus.Pending)]
    [InlineData(InitiationSessionStatus.Completed)]
    [InlineData(InitiationSessionStatus.Expired)]
    public async Task Existing_membership_is_never_reenrolled_even_after_terminal_session(string status)
    {
        var f = new Fixture();
        await f.SeedAsync(status: status);
        Assert.Equal(InitiationOutcome.Skipped, await f.RecoverAsync());
        Assert.Equal(0, f.Actions.Rituals);
    }

    [Fact]
    public async Task Fresh_rejoin_supersedes_old_pending_session_once()
    {
        var f = new Fixture();
        var previous = await f.SeedAsync();
        f.Actions.Member = f.Actions.Member! with { JoinedAtUtc = f.Now.AddHours(-1) };
        Assert.Equal(InitiationOutcome.Created, await f.RecoverAsync());
        Assert.Equal(InitiationSessionStatus.Expired, (await f.ReadAsync(previous.Id))!.Status);
        Assert.Equal(InitiationOutcome.Skipped, await f.RecoverAsync());
        Assert.Equal(1, f.Actions.Rituals);
        Assert.NotEqual(previous.Id, (await f.Sessions.GetLatestSessionAsync(Fixture.User, Fixture.Guild))!.Id);
    }

    [Fact]
    public async Task Welcome_failure_does_not_lose_persisted_recovery_session()
    {
        var f = new Fixture();
        f.Actions.FailOperation = "welcome";
        Assert.Equal(InitiationOutcome.Created, await f.RecoverAsync());
        Assert.NotNull(await f.Sessions.GetLatestSessionAsync(Fixture.User, Fixture.Guild));
        Assert.Equal(InitiationOutcome.Skipped, await f.RecoverAsync());
        Assert.Equal(1, f.Actions.Rituals);
    }

    [Fact]
    public async Task Recovery_database_failure_removes_orphan_ritual_and_allows_retry()
    {
        var f = new Fixture();
        f.Fault.FailNextSave();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.RecoverAsync());
        Assert.Null(await f.Sessions.GetLatestSessionAsync(Fixture.User, Fixture.Guild));
        Assert.Equal(1, f.Actions.Deletions);
        Assert.Equal(InitiationOutcome.Created, await f.RecoverAsync());
        Assert.Equal(2, f.Actions.Rituals);
    }

    [Fact]
    public async Task Cancelled_member_lock_does_not_perform_actions()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Workflow.RecoverAsync(Fixture.Guild, Fixture.User, Fixture.Channel, f.Actions, cancellation.Token));
        Assert.Equal(0, f.Actions.Fetches);
    }

    private sealed class Fixture
    {
        public const ulong Guild = 100, User = 200, Message = 300, Channel = 400;
        public DateTime Now { get; } = DateTime.UtcNow;
        public SaveFault Fault { get; } = new();
        public ContextFactory Factory { get; }
        public InitiationService Sessions { get; }
        public InitiationWorkflow Workflow { get; }
        public FakeActions Actions { get; }

        public Fixture()
        {
            Factory = new ContextFactory(new DbContextOptionsBuilder<CultBotDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(Fault).Options);
            Sessions = new InitiationService(Factory);
            Workflow = new InitiationWorkflow(Sessions, new FixedClock(Now));
            Actions = new FakeActions(new InitiationMemberState(false, false, true, null, Now.AddHours(-73)));
        }

        public async Task<InitiationSession> SeedAsync(DateTime? joinTime = null, bool reminded = true, string status = InitiationSessionStatus.Pending)
        {
            var session = new InitiationSession
            {
                GuildId = Guild, UserId = User, RitualMessageId = Message, RitualChannelId = Channel,
                JoinTimeUtc = joinTime ?? Now.AddHours(-72), Status = status,
                ReminderSentAt = reminded ? Now.AddHours(-BotConfig.ReminderGracePeriodHours - 1) : null
            };
            await using var context = Factory.CreateDbContext();
            context.InitiationSessions.Add(session);
            await context.SaveChangesAsync();
            return session;
        }

        public Task<InitiationSession?> ReadAsync(int id) => Sessions.GetSessionAsync(id);
        public Task<InitiationOutcome> CompleteAsync(string role = "SilentWitness") => Workflow.CompleteAsync(Guild, User, Message, role, Actions);
        public Task<InitiationOutcome> RecoverAsync() => Workflow.RecoverAsync(Guild, User, Channel, Actions);
    }

    private sealed class ContextFactory(DbContextOptions<CultBotDbContext> options) : IDbContextFactory<CultBotDbContext>
    {
        public CultBotDbContext CreateDbContext() => new(options);
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }

    private sealed class SaveFault : SaveChangesInterceptor
    {
        private int _failNext;
        public void FailNextSave() => Interlocked.Exchange(ref _failNext, 1);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _failNext, 0) == 1)
                throw new InvalidOperationException("Simulated database write failure");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FakeActions(InitiationMemberState? member) : IInitiationActions
    {
        public InitiationMemberState? Member { get; set; } = member;
        public bool ConfigurationSafe { get; set; } = true;
        public bool ReminderDelivered { get; set; } = true;
        public string? FailOperation { get; set; }
        public Func<int, Task>? OnFetch { get; set; }
        public Func<Task>? BeforeGrant { get; set; }
        public Func<Task>? BeforeOptional { get; set; }
        public int Fetches, RoleGrants, HoldingGrants, HoldingRemovals, Rituals, Deletions, Announcements, Reminders, Kicks, ExpirationAnnouncements;

        public bool IsConfigurationSafe() => ConfigurationSafe;
        public async Task<InitiationMemberState?> GetMemberAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Fetches++;
            if (OnFetch != null) await OnFetch(Fetches);
            return Member;
        }
        public async Task AddPathRoleAsync(string roleKey)
        {
            RoleGrants++;
            if (BeforeGrant != null) await BeforeGrant();
            Fail("role");
            Member = Member! with { PathRole = roleKey };
        }
        public Task AddHoldingRoleAsync() { HoldingGrants++; Member = Member! with { HasHoldingRole = true }; return Task.CompletedTask; }
        public async Task RemoveHoldingRoleAsync() { HoldingRemovals++; await Optional("holding cleanup"); Member = Member! with { HasHoldingRole = false }; }
        public Task<ulong> SendRitualAsync() { Rituals++; Fail("ritual"); return Task.FromResult(Fixture.Message + (ulong)Rituals); }
        public Task SendWelcomeAsync() { Fail("welcome"); return Task.CompletedTask; }
        public async Task DeleteRitualAsync(ulong messageId) { Deletions++; await Optional("ritual cleanup"); }
        public async Task AnnounceCompletionAsync(string roleKey) { Announcements++; await Optional("announcement"); }
        public Task<bool> SendReminderAsync() { Reminders++; return Task.FromResult(ReminderDelivered); }
        public Task KickAsync() { Kicks++; Fail("kick"); Member = null; return Task.CompletedTask; }
        public async Task AnnounceExpirationAsync() { ExpirationAnnouncements++; await Optional("expiration announcement"); }
        private async Task Optional(string operation) { if (BeforeOptional != null) await BeforeOptional(); Fail(operation); }
        private void Fail(string operation) { if (FailOperation == operation) throw new InvalidOperationException($"Simulated {operation} failure"); }
    }
}

public sealed class InitiationPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Each_missing_safety_requirement_disables_actions(int missing)
    {
        var requirements = Enumerable.Repeat(true, 6).ToArray();
        requirements[missing] = false;
        Assert.False(InitiationPolicy.IsConfigurationSafe(requirements[0], requirements[1], requirements[2], requirements[3], requirements[4], requirements[5], 24, 24, 7));
    }

    [Theory]
    [InlineData(0, 24, 7)]
    [InlineData(24, 0, 7)]
    [InlineData(24, 24, 0)]
    [InlineData(-1, 24, 7)]
    public void Invalid_time_windows_disable_actions(int timeout, int grace, int recovery) =>
        Assert.False(InitiationPolicy.IsConfigurationSafe(true, true, true, true, true, true, timeout, grace, recovery));

    [Fact]
    public void Valid_configuration_enables_actions() =>
        Assert.True(InitiationPolicy.IsConfigurationSafe(true, true, true, true, true, true, 24, 24, 7));

    [Fact]
    public void Newcomer_age_boundary_is_inclusive_and_old_members_are_excluded()
    {
        var now = DateTime.UtcNow;
        var member = new InitiationMemberState(false, false, true, null, now.AddDays(-7));
        Assert.True(InitiationPolicy.IsRecentNewcomer(member, now, 7));
        Assert.False(InitiationPolicy.IsRecentNewcomer(member with { JoinedAtUtc = now.AddDays(-7).AddTicks(-1) }, now, 7));
        Assert.False(InitiationPolicy.IsRecentNewcomer(member, now, 0));
    }
}
