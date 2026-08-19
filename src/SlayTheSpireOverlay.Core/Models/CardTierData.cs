namespace SlayTheSpireOverlay.Core.Models;

public record CardTierData(
    string CardId,
    string Tier,       // e.g., "S", "A", "B", "C", "D"
    double Score,      // e.g., 94.5
    string Commentary  // Evaluation notes
);
