using System.Text.Json;
using System.Text.Json.Serialization;
using WFHMonitor.Models;

namespace WFHMonitor.Infrastructure.Json;

public sealed class SafeApplicationUserJsonConverter : JsonConverter<ApplicationUser>
{
    public override ApplicationUser Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        throw new NotSupportedException("Application users cannot be created from API response payloads.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ApplicationUser value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("userName", value.UserName);
        writer.WriteString("email", value.Email);
        writer.WriteString("fullName", value.FullName);
        writer.WriteString("profilePhotoPath", value.ProfilePhotoPath);
        writer.WriteString("companyName", value.CompanyName);
        writer.WriteString("createdAt", value.CreatedAt);
        writer.WriteBoolean("isActive", value.IsActive);
        writer.WriteString("subscriptionPlan", value.SubscriptionPlan.ToString());
        writer.WriteBoolean("isProSubscriptionActive", value.IsProSubscriptionActive);

        if (value.ProSubscribedAt.HasValue)
            writer.WriteString("proSubscribedAt", value.ProSubscribedAt.Value);
        else
            writer.WriteNull("proSubscribedAt");

        if (value.ProSubscriptionEndsAt.HasValue)
            writer.WriteString("proSubscriptionEndsAt", value.ProSubscriptionEndsAt.Value);
        else
            writer.WriteNull("proSubscriptionEndsAt");

        writer.WriteBoolean("isProCancelAtPeriodEnd", value.IsProCancelAtPeriodEnd);
        writer.WriteString("organizationTeam", value.OrganizationTeam.ToString());

        if (value.OrgTeamId.HasValue)
            writer.WriteNumber("orgTeamId", value.OrgTeamId.Value);
        else
            writer.WriteNull("orgTeamId");

        if (value.OrgTeam is not null)
        {
            writer.WritePropertyName("orgTeam");
            writer.WriteStartObject();
            writer.WriteNumber("id", value.OrgTeam.Id);
            writer.WriteString("name", value.OrgTeam.Name);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("orgTeam");
        }

        if (value.LastActivityAt.HasValue)
            writer.WriteString("lastActivityAt", value.LastActivityAt.Value);
        else
            writer.WriteNull("lastActivityAt");

        writer.WriteEndObject();
    }
}
