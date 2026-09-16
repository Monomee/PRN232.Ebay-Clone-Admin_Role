using EbayClone.Application.ReturRequests.Commands.ApproveReturnRequest;
using EbayClone.Application.ReturRequests.Commands.RejectReturnRequest;
using EbayClone.Application.ReturRequests.Queries.GetReturnRequestDetail;
using EbayClone.Application.ReturRequests.Queries.GetReturnRequests;
using EbayClone.Application.ReturRequests.Commands.ProcessReturnLabel;
using EbayClone.Application.ReturRequests.Queries.GetReturnRequestMessages;

namespace EbayClone.Web.Endpoints;

public class AdminReturnRequests : EndpointGroupBase
{
    public override void Map(RouteGroupBuilder group)
    {
        // GET /api/adminreturnrequests?status=Pending  → Bước 1: Danh sách chờ xử lý
        group.MapGet(GetReturnRequests);

        // GET /api/adminreturnrequests/{id}           → Bước 3: Chi tiết yêu cầu
        group.MapGet("{id}", GetReturnRequestDetail);

        // GET /api/adminreturnrequests/{id}/messages   → Mới: Lấy tin nhắn làm bằng chứng
        group.MapGet("{id}/messages", GetReturnRequestMessages);

        // POST /api/adminreturnrequests/{id}/approve  → Bước 4: Chấp nhận hoàn tiền (giữ lại cho tương thích ngược)
        group.MapPost("{id}/approve", ApproveReturnRequest);

        // POST /api/adminreturnrequests/{id}/adjudicate → Bước 2: Phán quyết của admin
        group.MapPost("{id}/adjudicate", AdjudicateReturnRequest);

        // POST /api/adminreturnrequests/{id}/return-label → Bước 3: Cấp mã vận đơn
        group.MapPost("{id}/return-label", ProcessReturnLabel);

        // POST /api/adminreturnrequests/{id}/reject   → Bước 4: Từ chối hoàn tiền
        group.MapPost("{id}/reject", RejectReturnRequest);

        // POST /api/adminreturnrequests/{id}/freeze   → Mới: Đóng băng yêu cầu
        group.MapPost("{id}/freeze", FreezeReturnRequest);
    }

    // Bước 1: Danh sách — dùng [AsParameters] để map query string ?status=Pending
    public async Task<List<ReturnRequestListItemDto>> GetReturnRequests(
        ISender sender,
        [AsParameters] GetReturnRequestsQuery query)
    {
        return await sender.Send(query);
    }

    // Bước 3: Chi tiết một yêu cầu hoàn trả
    public async Task<ReturnRequestDetailDto> GetReturnRequestDetail(ISender sender, int id)
    {
        return await sender.Send(new GetReturnRequestDetailQuery(id));
    }

    // Lấy tin nhắn giữa Buyer và Seller làm bằng chứng
    public async Task<List<ReturnRequestMessageDto>> GetReturnRequestMessages(ISender sender, int id)
    {
        return await sender.Send(new GetReturnRequestMessagesQuery(id));
    }

    // Bước 4: Chấp nhận hoàn tiền
    public async Task<IResult> ApproveReturnRequest(
        ISender sender,
        int id,
        ApproveReturnRequestCommand command)
    {
        if (id != command.ReturnRequestId) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    // Bước 4: Từ chối hoàn tiền
    public async Task<IResult> RejectReturnRequest(
        ISender sender,
        int id,
        RejectReturnRequestCommand command)
    {
        if (id != command.ReturnRequestId) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    // Phán quyết từ Admin (Adjudication)
    public async Task<IResult> AdjudicateReturnRequest(
        ISender sender,
        int id,
        ApproveReturnRequestCommand command)
    {
        if (id != command.ReturnRequestId) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    // Cấp mã vận đơn trả hàng (Return Facilitation)
    public async Task<IResult> ProcessReturnLabel(
        ISender sender,
        int id,
        ProcessReturnLabelCommand command)
    {
        if (id != command.ReturnRequestId) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }

    // Đóng băng yêu cầu hoàn trả (Fraud Prevention)
    public async Task<IResult> FreezeReturnRequest(
        ISender sender,
        int id,
        EbayClone.Application.ReturRequests.Commands.FreezeReturnRequest.FreezeReturnRequestCommand command)
    {
        if (id != command.ReturnRequestId) return Results.BadRequest();
        await sender.Send(command);
        return Results.NoContent();
    }
}
