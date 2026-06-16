namespace vless_proxy.Contracts.Request.Invite;

public class CreateInviteRequest
{
    public string Description { get; set; } = string.Empty;
    public int ExpireDays { get; set; } = 7;
}
