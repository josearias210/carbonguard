using CarbonGuard.Core.Models;

namespace CarbonGuard.Core.Detection;

public interface IAnomalyDetector
{
    AnalysisResponse Analyze(AnalysisRequest request);
}

