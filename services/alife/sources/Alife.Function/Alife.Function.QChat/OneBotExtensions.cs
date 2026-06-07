namespace Alife.Function.QChat;

/// <summary>
/// 基础 API 扩展，提供最常用的消息发送与文件处理功能。
/// </summary>
public static class OneBotExtensions
{
    public static async Task SendPrivateMessage(this OneBotClient client, long userId, string message)
    {
        await client.SendActionAsync("send_private_msg", new { user_id = userId, message });
    }
    public static async Task SendGroupMessage(this OneBotClient client, long groupId, string message)
    {
        await client.SendActionAsync("send_group_msg", new { group_id = groupId, message });
    }
    public static async Task UploadPrivateFile(this OneBotClient client, long userId, string filePath, string name)
    {
        await client.SendActionAsync("upload_private_file", new UploadFileParams { UserId = userId, File = filePath, Name = name });
    }
    public static async Task UploadGroupFile(this OneBotClient client, long groupId, string filePath, string name)
    {
        await client.SendActionAsync("upload_group_file", new UploadFileParams { GroupId = groupId, File = filePath, Name = name });
    }

    /// <summary>
    /// 下载文件（用于私聊）
    /// </summary>
    public static async Task<OneBotFile?> GetPrivateFile(this OneBotClient client, string fileId)
    {
        return await client.CallActionAsync<OneBotFile>("get_file", new { file = fileId });
    }
    /// <summary>
    /// 获取群文件下载链接。
    /// </summary>
    public static async Task<OneBotFile?> GetGroupFileUrl(this OneBotClient client, long groupId, string fileId)
    {
        return await client.CallActionAsync<OneBotFile>("get_group_file_url", new { group_id = groupId, file_id = fileId });
    }
    /// <summary>
    /// 根据消息 ID 获取消息详情。
    /// </summary>
    public static async Task<OneBotMessageEvent?> GetMessage(this OneBotClient client, long messageId)
    {
        return await client.CallActionAsync<OneBotMessageEvent>("get_msg", new { message_id = messageId });
    }
    /// <summary>
    /// 获取合并转发消息详情。
    /// </summary>
    public static async Task<List<OneBotForwardMessage>?> GetForwardMessage(this OneBotClient client, string forwardId)
    {
        OneBotForwardData? data = await client.CallActionAsync<OneBotForwardData>("get_forward_msg", new { id = forwardId });
        return data?.Messages;
    }
}
