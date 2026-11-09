using System;
using System.Threading;
using System.Threading.Tasks;

namespace HongBeautyPOS.Application;

public sealed class ReportService(IReportsRepository repository)
{
	public Task<ReportSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
	{
		if (to < from || to.DayNumber - from.DayNumber > 366)
		{
			throw new ArgumentException("Khoảng báo cáo phải từ 1 đến 367 ngày.");
		}
		return repository.SummaryAsync(from, to, cancellationToken);
	}
}
