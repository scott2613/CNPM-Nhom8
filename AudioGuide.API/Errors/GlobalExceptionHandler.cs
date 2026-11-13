using AudioGuide.Core.DTOs;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
namespace AudioGuide.API.Errors;
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
 public async ValueTask<bool> TryHandleAsync(HttpContext context,Exception error,CancellationToken ct){
  if(error is OperationCanceledException&&context.RequestAborted.IsCancellationRequested){context.Response.StatusCode=499;return true;}
  var status=error switch{DomainException e=>e.Status,ValidationException=>400,DbUpdateConcurrencyException=>409,DbUpdateException{InnerException:SqlException{Number:2601 or 2627}}=>409,_=>500};
  logger.LogError(error,"Request failed {TraceId}",context.TraceIdentifier);
  context.Response.StatusCode=status;
  await context.Response.WriteAsJsonAsync(new {success=false,message=error is DomainException or ValidationException?error.Message:status==409?"Dữ liệu bị trùng hoặc đã thay đổi. Hãy tải lại.":"Không thể xử lý yêu cầu. Vui lòng thử lại.",traceId=context.TraceIdentifier},ct);
  return true;
 }
}
