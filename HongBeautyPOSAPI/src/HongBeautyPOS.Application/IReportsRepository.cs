using System;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public interface IReportsRepository
{
	Task<ReportSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
