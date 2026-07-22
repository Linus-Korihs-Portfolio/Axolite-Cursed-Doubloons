using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PCG.RoomAssembler.Metrics
{
    public static class PCGGenerationMetricsCsvExporter
    {
        private const char Separator = ';';
        private static readonly CultureInfo NumberCulture = CultureInfo.GetCultureInfo("de-DE");
        private static readonly Encoding CsvEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

        private static readonly string[] RunHeaders =
        {
            "Run",
            "Zeitpunkt UTC",
            "Generator",
            "Seed Start",
            "Seed Verwendet",
            "Erfolg",
            "Fehlerkategorie",
            "Emergency Fallback",
            "Fehlversuche",
            "Versuche Gesamt",
            "Candidate Placement Attempts",
            "Unfillable Sockets",
            "Soll Min Raeume",
            "Soll Max Raeume",
            "Soll Min Bossdistanz",
            "Effektiv Min Raeume",
            "Effektiv Max Raeume",
            "Raeume vor Capping",
            "Raeume nach Capping",
            "Offene Sockets vor Capping",
            "Offene Sockets nach Capping",
            "Caps Gesamt",
            "Dead-End Caps",
            "Wall Caps",
            "Logical-Only Caps",
            "Start zu Boss Distanz",
            "Kritischer Pfad Raeume",
            "Nebenpfad Raeume",
            "Branching Factor",
            "Spezialraeume valide",
            "Ungueltige Spezialraeume",
            "Gesamtdauer ms",
            "Room Placement ms",
            "Capping ms",
            "NavMesh ms",
            "Content Spawning ms"
        };

        public static string Export(
            IReadOnlyList<PCGGenerationMetrics> rows,
            string outputDirectory,
            string filePrefix)
        {
            string directory = ResolveOutputDirectory(outputDirectory);
            Directory.CreateDirectory(directory);

            string safePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "pcg_metrics" : filePrefix.Trim();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

            string analysisCsvPath = Path.Combine(directory, $"{safePrefix}_analysis_summary_{timestamp}.csv");
            string analysisMarkdownPath = Path.Combine(directory, $"{safePrefix}_analysis_report_{timestamp}.md");

            File.WriteAllText(analysisCsvPath, BuildAnalysisSummaryCsv(rows), CsvEncoding);
            File.WriteAllText(analysisMarkdownPath, BuildAnalysisReportMarkdown(rows), new UTF8Encoding(false));

            return analysisCsvPath;
        }

        public static string BuildCsv(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            return BuildRunSummaryCsv(rows);
        }

        public static string BuildRunSummaryCsv(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            var builder = new StringBuilder();
            AppendRow(builder, RunHeaders);

            if (rows == null)
                return builder.ToString();

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                AppendRow(builder, new[]
                {
                    Int(row.runIndex),
                    row.timestampUtc,
                    row.generatorName,
                    Int(row.initialSeed),
                    Int(row.seed),
                    Bool(row.success),
                    row.failureCategory,
                    Bool(row.emergencyFallbackUsed),
                    Int(row.failedFullAttempts),
                    Int(row.fullAttemptsUsed),
                    Int(row.candidatePlacementAttempts),
                    Int(row.unfillableSockets),
                    Int(row.configuredMinRooms),
                    Int(row.configuredMaxRooms),
                    Int(row.configuredMinBossDistanceRooms),
                    Int(row.effectiveMinRooms),
                    Int(row.effectiveMaxRooms),
                    Int(row.actualRoomsBeforeCapping),
                    Int(row.actualRoomsAfterCapping),
                    Int(row.openSocketsBeforeCapping),
                    Int(row.remainingOpenSocketsAfterCapping),
                    Int(row.capPlacements),
                    Int(row.deadEndCaps),
                    Int(row.wallCaps),
                    Int(row.logicalOnlyCaps),
                    Int(row.startToBossGraphDistance),
                    Int(row.criticalPathRooms),
                    Int(row.sidePathRooms),
                    Float(row.branchingFactor),
                    Bool(row.specialRoomPlacementValid),
                    Int(row.invalidSpecialRoomCount),
                    Double(row.totalGenerationMs),
                    Double(row.roomPlacementMs),
                    Double(row.cappingMs),
                    Double(row.navMeshMs),
                    Double(row.contentSpawningMs)
                });
            }

            return builder.ToString();
        }

        public static string BuildAnalysisSummaryCsv(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            AnalysisSummary summary = Analyze(rows);
            var builder = new StringBuilder();

            AppendRow(builder, new[]
            {
                "Gruppe",
                "Kennzahl",
                "Wert",
                "Bewertung"
            });

            AppendAnalysisRow(builder, "Versuchsdesign", "Varianten-ID", FormatCountsInline(summary.variantTotals), string.Empty);
            AppendAnalysisRow(builder, "Zuverlaessigkeit", "Runs gesamt", Int(summary.totalRuns), string.Empty);
            AppendAnalysisRow(builder, "Zuverlaessigkeit", "Erfolgreiche Runs", Int(summary.successfulRuns), string.Empty);
            AppendAnalysisRow(builder, "Zuverlaessigkeit", "Success / Failure", Percent(summary.successRate), RateLabel(summary.successRate, 0.95, 0.8));
            AppendAnalysisRow(builder, "Zuverlaessigkeit", "Durchschnittliche Fehlversuche", Double(summary.averageFailedAttempts), RetryLabel(summary.averageFailedAttempts));
            AppendAnalysisRow(builder, "Zuverlaessigkeit", "Emergency-Fallback-Rate", Percent(summary.emergencyFallbackRate), FallbackLabel(summary.emergencyFallbackRate));

            AppendAnalysisRow(builder, "Layout", "Durchschnitt Raeume vor Capping", Double(summary.averageRoomsBeforeCapping), string.Empty);
            AppendAnalysisRow(builder, "Layout", "Final Placed Rooms", Double(summary.averageRoomsAfterCapping), string.Empty);
            AppendAnalysisRow(builder, "Layout", "Sollraum-Trefferquote", Percent(summary.roomTargetHitRate), RateLabel(summary.roomTargetHitRate, 0.9, 0.75));
            AppendAnalysisRow(builder, "Layout", "Open Sockets Pre-Cap", Double(summary.averageOpenSocketsBeforeCapping), string.Empty);
            AppendAnalysisRow(builder, "Layout", "Durchschnitt offene Sockets nach Capping", Double(summary.averageRemainingOpenSockets), SocketLabel(summary.averageRemainingOpenSockets));
            AppendAnalysisRow(builder, "Layout", "Durchschnitt Branching Factor", Double(summary.averageBranchingFactor), BranchingLabel(summary.averageBranchingFactor));
            AppendAnalysisRow(builder, "Layout", "Durchschnitt kritischer Pfad", Double(summary.averageCriticalPathRooms), string.Empty);
            AppendAnalysisRow(builder, "Layout", "Durchschnitt Nebenpfad-Raeume", Double(summary.averageSidePathRooms), string.Empty);

            AppendAnalysisRow(builder, "Progression", "Durchschnitt Start-Boss-Distanz", Double(summary.averageBossDistance), BossDistanceLabel(summary.averageBossDistance, summary.averageConfiguredMinBossDistance));
            AppendAnalysisRow(builder, "Progression", "Boss-Distanz gueltig", Percent(summary.bossDistanceValidRate), RateLabel(summary.bossDistanceValidRate, 0.9, 0.75));
            AppendAnalysisRow(builder, "Progression", "Spezialraum-Validitaet", Percent(summary.specialRoomValidRate), RateLabel(summary.specialRoomValidRate, 0.95, 0.8));

            AppendAnalysisRow(builder, "Performance", "Gesamtdauer ms", Double(summary.averageTotalMs), PerformanceLabel(summary.averageTotalMs));
            AppendAnalysisRow(builder, "Performance", "Durchschnitt Room Placement ms", Double(summary.averagePlacementMs), string.Empty);
            AppendAnalysisRow(builder, "Performance", "Durchschnitt Capping ms", Double(summary.averageCappingMs), string.Empty);
            AppendAnalysisRow(builder, "Performance", "Durchschnitt NavMesh ms", Double(summary.averageNavMeshMs), string.Empty);
            AppendAnalysisRow(builder, "Performance", "Durchschnitt Content Spawning ms", Double(summary.averageContentMs), string.Empty);

            AppendAnalysisRow(builder, "Fehler", "Error Cause", FormatCountsInline(summary.errorCauseTotals), string.Empty);
            AppendTopCounts(builder, "Raumtypen", summary.roomTypeTotals);
            AppendTopCounts(builder, "Generation Fehler", summary.generationFailureTotals);
            AppendTopCounts(builder, "Placement Fehler", summary.placementFailureTotals);
            AppendRunCoreRows(builder, rows);

            return builder.ToString();
        }

        public static string BuildAnalysisReportMarkdown(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            AnalysisSummary summary = Analyze(rows);
            var builder = new StringBuilder();

            builder.AppendLine("# PCG-Auswertung");
            builder.AppendLine();
            builder.AppendLine($"Varianten-ID: {FormatCountsInline(summary.variantTotals)}");
            builder.AppendLine($"Runs gesamt: {Int(summary.totalRuns)}");
            builder.AppendLine($"Erfolgsrate: {Percent(summary.successRate)} ({Int(summary.successfulRuns)} erfolgreich, {Int(summary.failedRuns)} fehlgeschlagen)");
            builder.AppendLine($"Durchschnittliche Fehlversuche: {Double(summary.averageFailedAttempts)}");
            builder.AppendLine($"Emergency-Fallback-Rate: {Percent(summary.emergencyFallbackRate)}");
            builder.AppendLine();

            builder.AppendLine("## Kurzbewertung");
            builder.AppendLine();
            AppendFinding(builder, "Zuverlaessigkeit", RateLabel(summary.successRate, 0.95, 0.8));
            AppendFinding(builder, "Retry-Aufwand", RetryLabel(summary.averageFailedAttempts));
            AppendFinding(builder, "Socket-Abschluss", SocketLabel(summary.averageRemainingOpenSockets));
            AppendFinding(builder, "Verzweigung", BranchingLabel(summary.averageBranchingFactor));
            AppendFinding(builder, "Boss-Progression", BossDistanceLabel(summary.averageBossDistance, summary.averageConfiguredMinBossDistance));
            AppendFinding(builder, "Spezialraeume", RateLabel(summary.specialRoomValidRate, 0.95, 0.8));
            AppendFinding(builder, "Performance", PerformanceLabel(summary.averageTotalMs));
            builder.AppendLine();

            builder.AppendLine("## Layout-Kennzahlen");
            builder.AppendLine();
            builder.AppendLine("| Kennzahl | Wert |");
            builder.AppendLine("| --- | ---: |");
            builder.AppendLine($"| Raeume vor Capping, Durchschnitt | {Double(summary.averageRoomsBeforeCapping)} |");
            builder.AppendLine($"| Final Placed Rooms, Durchschnitt | {Double(summary.averageRoomsAfterCapping)} |");
            builder.AppendLine($"| Sollraum-Trefferquote | {Percent(summary.roomTargetHitRate)} |");
            builder.AppendLine($"| Open Sockets Pre-Cap, Durchschnitt | {Double(summary.averageOpenSocketsBeforeCapping)} |");
            builder.AppendLine($"| Offene Sockets nach Capping, Durchschnitt | {Double(summary.averageRemainingOpenSockets)} |");
            builder.AppendLine($"| Branching Factor, Durchschnitt | {Double(summary.averageBranchingFactor)} |");
            builder.AppendLine($"| Start-Boss-Distanz, Durchschnitt | {Double(summary.averageBossDistance)} |");
            builder.AppendLine($"| Boss-Distanz gueltig | {Percent(summary.bossDistanceValidRate)} |");
            builder.AppendLine($"| Spezialraum-Validitaet | {Percent(summary.specialRoomValidRate)} |");
            builder.AppendLine();

            AppendRunCoreMarkdown(builder, rows);

            builder.AppendLine("## Performance");
            builder.AppendLine();
            builder.AppendLine("| Phase | Durchschnitt ms |");
            builder.AppendLine("| --- | ---: |");
            builder.AppendLine($"| Gesamt | {Double(summary.averageTotalMs)} |");
            builder.AppendLine($"| Room Placement | {Double(summary.averagePlacementMs)} |");
            builder.AppendLine($"| Capping | {Double(summary.averageCappingMs)} |");
            builder.AppendLine($"| NavMesh | {Double(summary.averageNavMeshMs)} |");
            builder.AppendLine($"| Content Spawning | {Double(summary.averageContentMs)} |");
            builder.AppendLine();

            AppendMarkdownCounts(builder, "Raumtyp-Verteilung", summary.roomTypeTotals);
            AppendMarkdownCounts(builder, "Error Causes", summary.errorCauseTotals);
            AppendMarkdownCounts(builder, "Generation-Fehler", summary.generationFailureTotals);
            AppendMarkdownCounts(builder, "Placement-Fehler", summary.placementFailureTotals);

            builder.AppendLine("## Interpretation fuer die Arbeit");
            builder.AppendLine();
            builder.AppendLine(BuildResearchInterpretation(summary));

            return builder.ToString();
        }

        public static string BuildRoomTypeCsv(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            var builder = new StringBuilder();
            AppendRow(builder, new[]
            {
                "Run",
                "Seed",
                "Raumtyp",
                "Anzahl"
            });

            if (rows == null)
                return builder.ToString();

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                foreach (KeyValuePair<string, int> pair in SortCounts(row.roomTypeFrequency))
                {
                    AppendRow(builder, new[]
                    {
                        Int(row.runIndex),
                        Int(row.seed),
                        pair.Key,
                        Int(pair.Value)
                    });
                }
            }

            return builder.ToString();
        }

        public static string BuildFailureCsv(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            var builder = new StringBuilder();
            AppendRow(builder, new[]
            {
                "Run",
                "Seed",
                "Gruppe",
                "Kategorie",
                "Anzahl"
            });

            if (rows == null)
                return builder.ToString();

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                AppendFailureRows(builder, row, "Generation", row.generationFailureCounts);
                AppendFailureRows(builder, row, "Placement", row.placementFailureCounts);
            }

            return builder.ToString();
        }

        public static string BuildSpecialRoomCsv(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            var builder = new StringBuilder();
            AppendRow(builder, new[]
            {
                "Run",
                "Seed",
                "Spezialraum",
                "Room Id",
                "Graph Degree",
                "Graph Distance",
                "Dead-End Pflicht",
                "Dead-End gueltig",
                "Distanz gueltig",
                "Gesamt gueltig"
            });

            if (rows == null)
                return builder.ToString();

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                for (int s = 0; s < row.specialRooms.Count; s++)
                {
                    PCGSpecialRoomMetric special = row.specialRooms[s];
                    if (special == null) continue;

                    AppendRow(builder, new[]
                    {
                        Int(row.runIndex),
                        Int(row.seed),
                        special.kind,
                        special.roomId,
                        Int(special.graphDegree),
                        Int(special.graphDistance),
                        Bool(special.requiresDeadEnd),
                        Bool(special.deadEndValid),
                        Bool(special.distanceValid),
                        Bool(special.valid)
                    });
                }
            }

            return builder.ToString();
        }

        private static void AppendFailureRows(
            StringBuilder builder,
            PCGGenerationMetrics row,
            string group,
            Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> pair in SortCounts(counts))
            {
                AppendRow(builder, new[]
                {
                    Int(row.runIndex),
                    Int(row.seed),
                    group,
                    pair.Key,
                    Int(pair.Value)
                });
            }
        }

        private static AnalysisSummary Analyze(IReadOnlyList<PCGGenerationMetrics> rows)
        {
            AnalysisSummary summary = new AnalysisSummary();
            if (rows == null || rows.Count == 0)
                return summary;

            double failedAttempts = 0d;
            double roomsBeforeCapping = 0d;
            double roomsAfterCapping = 0d;
            double openSocketsBeforeCapping = 0d;
            double remainingOpenSockets = 0d;
            double branchingFactor = 0d;
            double criticalPathRooms = 0d;
            double sidePathRooms = 0d;
            double totalMs = 0d;
            double placementMs = 0d;
            double cappingMs = 0d;
            double navMeshMs = 0d;
            double contentMs = 0d;
            double configuredMinBossDistance = 0d;
            double bossDistance = 0d;
            int bossDistanceSamples = 0;
            int bossDistanceValid = 0;
            int roomTargetHits = 0;
            int specialRoomValid = 0;
            int fallbackRuns = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                summary.totalRuns++;
                if (row.success) summary.successfulRuns++;
                else summary.failedRuns++;

                if (row.emergencyFallbackUsed) fallbackRuns++;
                if (row.specialRoomPlacementValid) specialRoomValid++;

                IncrementCount(summary.variantTotals, string.IsNullOrWhiteSpace(row.variantId) ? "Standard" : row.variantId);
                if (!row.success)
                    IncrementCount(summary.errorCauseTotals, FormatRunErrorCause(row));

                failedAttempts += row.failedFullAttempts;
                roomsBeforeCapping += row.actualRoomsBeforeCapping;
                roomsAfterCapping += row.actualRoomsAfterCapping;
                openSocketsBeforeCapping += row.openSocketsBeforeCapping;
                remainingOpenSockets += row.remainingOpenSocketsAfterCapping;
                branchingFactor += row.branchingFactor;
                criticalPathRooms += row.criticalPathRooms;
                sidePathRooms += row.sidePathRooms;
                totalMs += row.totalGenerationMs;
                placementMs += row.roomPlacementMs;
                cappingMs += row.cappingMs;
                navMeshMs += row.navMeshMs;
                contentMs += row.contentSpawningMs;
                configuredMinBossDistance += row.configuredMinBossDistanceRooms;

                if (row.actualRoomsBeforeCapping >= row.configuredMinRooms
                    && row.actualRoomsBeforeCapping <= row.configuredMaxRooms)
                {
                    roomTargetHits++;
                }

                if (row.startToBossGraphDistance >= 0)
                {
                    bossDistance += row.startToBossGraphDistance;
                    bossDistanceSamples++;

                    if (row.startToBossGraphDistance >= row.configuredMinBossDistanceRooms)
                        bossDistanceValid++;
                }

                AddCounts(summary.roomTypeTotals, row.roomTypeFrequency);
                AddCounts(summary.generationFailureTotals, row.generationFailureCounts);
                AddCounts(summary.placementFailureTotals, row.placementFailureCounts);
            }

            if (summary.totalRuns == 0)
                return summary;

            double runCount = summary.totalRuns;
            summary.successRate = summary.successfulRuns / runCount;
            summary.emergencyFallbackRate = fallbackRuns / runCount;
            summary.averageFailedAttempts = failedAttempts / runCount;
            summary.averageRoomsBeforeCapping = roomsBeforeCapping / runCount;
            summary.averageRoomsAfterCapping = roomsAfterCapping / runCount;
            summary.averageOpenSocketsBeforeCapping = openSocketsBeforeCapping / runCount;
            summary.averageRemainingOpenSockets = remainingOpenSockets / runCount;
            summary.averageBranchingFactor = branchingFactor / runCount;
            summary.averageCriticalPathRooms = criticalPathRooms / runCount;
            summary.averageSidePathRooms = sidePathRooms / runCount;
            summary.averageTotalMs = totalMs / runCount;
            summary.averagePlacementMs = placementMs / runCount;
            summary.averageCappingMs = cappingMs / runCount;
            summary.averageNavMeshMs = navMeshMs / runCount;
            summary.averageContentMs = contentMs / runCount;
            summary.roomTargetHitRate = roomTargetHits / runCount;
            summary.specialRoomValidRate = specialRoomValid / runCount;
            summary.averageConfiguredMinBossDistance = configuredMinBossDistance / runCount;

            if (bossDistanceSamples > 0)
            {
                summary.averageBossDistance = bossDistance / bossDistanceSamples;
                summary.bossDistanceValidRate = bossDistanceValid / (double)bossDistanceSamples;
            }

            return summary;
        }

        private static void AddCounts(
            Dictionary<string, int> target,
            Dictionary<string, int> source)
        {
            if (target == null || source == null) return;

            foreach (KeyValuePair<string, int> pair in source)
            {
                if (target.TryGetValue(pair.Key, out int current))
                    target[pair.Key] = current + pair.Value;
                else
                    target.Add(pair.Key, pair.Value);
            }
        }

        private static void IncrementCount(Dictionary<string, int> target, string key)
        {
            if (target == null) return;
            if (string.IsNullOrWhiteSpace(key)) key = "Unknown";

            if (target.TryGetValue(key, out int current))
                target[key] = current + 1;
            else
                target.Add(key, 1);
        }

        private static void AppendAnalysisRow(
            StringBuilder builder,
            string group,
            string metric,
            string value,
            string rating)
        {
            AppendRow(builder, new[] { group, metric, value, rating });
        }

        private static void AppendTopCounts(
            StringBuilder builder,
            string group,
            Dictionary<string, int> counts)
        {
            foreach (KeyValuePair<string, int> pair in SortCounts(counts))
                AppendAnalysisRow(builder, group, pair.Key, Int(pair.Value), string.Empty);
        }

        private static void AppendRunCoreRows(
            StringBuilder builder,
            IReadOnlyList<PCGGenerationMetrics> rows)
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                string group = $"Run {row.runIndex}";
                AppendAnalysisRow(builder, group, "Varianten-ID", string.IsNullOrWhiteSpace(row.variantId) ? "Standard" : row.variantId, string.Empty);
                AppendAnalysisRow(builder, group, "Success / Failure", Bool(row.success), row.success ? "Success" : "Failure");
                AppendAnalysisRow(builder, group, "Gesamtdauer ms", Double(row.totalGenerationMs), PerformanceLabel(row.totalGenerationMs));
                AppendAnalysisRow(builder, group, "Final Placed Rooms", Int(row.actualRoomsAfterCapping), string.Empty);
                AppendAnalysisRow(builder, group, "Open Sockets Pre-Cap", Int(row.openSocketsBeforeCapping), string.Empty);
                AppendAnalysisRow(builder, group, "Error Cause", FormatRunErrorCause(row), string.Empty);
            }
        }

        private static void AppendRunCoreMarkdown(
            StringBuilder builder,
            IReadOnlyList<PCGGenerationMetrics> rows)
        {
            builder.AppendLine("## Kernwerte pro Run");
            builder.AppendLine();

            if (rows == null || rows.Count == 0)
            {
                builder.AppendLine("Keine Run-Daten.");
                builder.AppendLine();
                return;
            }

            builder.AppendLine("| Run | Varianten-ID | Success | Gesamtdauer ms | Final Placed Rooms | Open Sockets Pre-Cap | Error Cause |");
            builder.AppendLine("| ---: | --- | --- | ---: | ---: | ---: | --- |");

            for (int i = 0; i < rows.Count; i++)
            {
                PCGGenerationMetrics row = rows[i];
                if (row == null) continue;

                builder.Append("| ");
                builder.Append(Int(row.runIndex));
                builder.Append(" | ");
                builder.Append(string.IsNullOrWhiteSpace(row.variantId) ? "Standard" : EscapeMarkdownCell(row.variantId));
                builder.Append(" | ");
                builder.Append(Bool(row.success));
                builder.Append(" | ");
                builder.Append(Double(row.totalGenerationMs));
                builder.Append(" | ");
                builder.Append(Int(row.actualRoomsAfterCapping));
                builder.Append(" | ");
                builder.Append(Int(row.openSocketsBeforeCapping));
                builder.Append(" | ");
                builder.Append(EscapeMarkdownCell(FormatRunErrorCause(row)));
                builder.AppendLine(" |");
            }

            builder.AppendLine();
        }

        private static void AppendFinding(StringBuilder builder, string label, string value)
        {
            builder.Append("- ");
            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(value);
        }

        private static void AppendMarkdownCounts(
            StringBuilder builder,
            string title,
            Dictionary<string, int> counts)
        {
            builder.Append("## ");
            builder.AppendLine(title);
            builder.AppendLine();

            List<KeyValuePair<string, int>> sorted = SortCounts(counts);
            if (sorted.Count == 0)
            {
                builder.AppendLine("Keine Eintraege.");
                builder.AppendLine();
                return;
            }

            builder.AppendLine("| Kategorie | Anzahl |");
            builder.AppendLine("| --- | ---: |");

            for (int i = 0; i < sorted.Count; i++)
            {
                KeyValuePair<string, int> pair = sorted[i];
                builder.Append("| ");
                builder.Append(pair.Key);
                builder.Append(" | ");
                builder.Append(Int(pair.Value));
                builder.AppendLine(" |");
            }

            builder.AppendLine();
        }

        private static string FormatRunErrorCause(PCGGenerationMetrics row)
        {
            if (row == null) return "Unknown";
            if (row.success) return "None";

            string generation = TopCountKey(row.generationFailureCounts);
            string placement = TopCountKey(row.placementFailureCounts);
            string primary = !string.IsNullOrWhiteSpace(row.failureCategory) && row.failureCategory != "None"
                ? row.failureCategory
                : generation;

            if (string.IsNullOrWhiteSpace(primary))
                primary = "UnknownFailure";

            if (!string.IsNullOrWhiteSpace(placement))
                return $"{primary} ({placement})";

            return primary;
        }

        private static string TopCountKey(Dictionary<string, int> counts)
        {
            List<KeyValuePair<string, int>> sorted = SortCounts(counts);
            return sorted.Count > 0 ? sorted[0].Key : string.Empty;
        }

        private static string FormatCountsInline(Dictionary<string, int> counts)
        {
            List<KeyValuePair<string, int>> sorted = SortCounts(counts);
            if (sorted.Count == 0) return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(sorted[i].Key);
                builder.Append('=');
                builder.Append(Int(sorted[i].Value));
            }

            return builder.ToString();
        }

        private static string EscapeMarkdownCell(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("|", "\\|");
        }

        private static string BuildResearchInterpretation(AnalysisSummary summary)
        {
            if (summary.totalRuns == 0)
                return "Es wurden keine Runs ausgewertet.";

            var builder = new StringBuilder();

            builder.Append("Die gemessene Erfolgsrate liegt bei ");
            builder.Append(Percent(summary.successRate));
            builder.Append(". ");

            if (summary.successRate >= 0.95)
            {
                builder.Append("Das spricht fuer eine hohe algorithmische Zuverlaessigkeit der regelbasierten Generierung. ");
            }
            else if (summary.successRate >= 0.8)
            {
                builder.Append("Das spricht fuer grundsaetzlich stabile Generierung, aber einzelne Regelkonflikte oder zu enge Constraints bleiben sichtbar. ");
            }
            else
            {
                builder.Append("Das weist auf deutliche Constraint-Probleme hin; fuer die Arbeit waere das ein Hinweis, dass Kontrolle aktuell zu Lasten robuster Zufallsgenerierung geht. ");
            }

            builder.Append("Der durchschnittliche Branching Factor betraegt ");
            builder.Append(Double(summary.averageBranchingFactor));
            builder.Append(", damit wirkt das Layout ");
            builder.Append(BranchingLabel(summary.averageBranchingFactor).ToLowerInvariant());
            builder.Append(". ");

            if (summary.averageRemainingOpenSockets <= 0d)
            {
                builder.Append("Die Capping-Regeln schliessen offene Sockets im Mittel vollstaendig, was die Validitaet des erzeugten Levels unterstuetzt. ");
            }
            else
            {
                builder.Append("Offene Sockets bleiben nach dem Capping messbar, daher sollten Socket-Regeln, Cap-Prefabs oder MaxCapsAfterEnd untersucht werden. ");
            }

            builder.Append("Die Spezialraum-Validitaet liegt bei ");
            builder.Append(Percent(summary.specialRoomValidRate));
            builder.Append("; dieser Wert ist besonders relevant fuer die Isaac-nahe Progressionskontrolle und fuer die Ableitung auf Axolite: Cursed Doubloons.");

            return builder.ToString();
        }

        private static string RateLabel(double rate, double good, double acceptable)
        {
            if (rate >= good) return "hoch / stabil";
            if (rate >= acceptable) return "brauchbar, aber beobachtbar instabil";
            return "kritisch";
        }

        private static string RetryLabel(double averageRetries)
        {
            if (averageRetries <= 0.25d) return "sehr wenig Retry-Aufwand";
            if (averageRetries <= 1.5d) return "moderater Retry-Aufwand";
            return "hoher Retry-Aufwand, Constraints vermutlich zu eng";
        }

        private static string FallbackLabel(double rate)
        {
            if (rate <= 0.05d) return "Fallback kaum noetig";
            if (rate <= 0.2d) return "Fallback gelegentlich noetig";
            return "Fallback haeufig noetig, normale Regeln pruefen";
        }

        private static string SocketLabel(double averageRemainingOpenSockets)
        {
            if (averageRemainingOpenSockets <= 0d) return "vollstaendig geschlossen";
            if (averageRemainingOpenSockets <= 1d) return "kleine Restprobleme";
            return "kritisch, offene Sockets bleiben bestehen";
        }

        private static string BranchingLabel(double averageBranchingFactor)
        {
            if (averageBranchingFactor < 0.3d) return "eher schlauchfoermig";
            if (averageBranchingFactor <= 0.8d) return "ausgewogen kontrolliert";
            return "stark verzweigt / labyrinthartig";
        }

        private static string BossDistanceLabel(double averageBossDistance, double averageConfiguredMin)
        {
            if (averageBossDistance < 0d) return "keine Bossdistanz messbar";
            if (averageConfiguredMin <= 0d) return "Bossdistanz messbar";
            return averageBossDistance >= averageConfiguredMin
                ? "Progression ausreichend gestreckt"
                : "Boss liegt im Mittel zu nah am Start";
        }

        private static string PerformanceLabel(double averageTotalMs)
        {
            if (averageTotalMs <= 50d) return "sehr schnell";
            if (averageTotalMs <= 250d) return "unproblematisch";
            if (averageTotalMs <= 1000d) return "spuerbar, aber fuer Offline-Generierung brauchbar";
            return "teuer, Performance pruefen";
        }

        private static string ResolveOutputDirectory(string outputDirectory)
        {
            if (!string.IsNullOrWhiteSpace(outputDirectory) && Path.IsPathRooted(outputDirectory))
                return outputDirectory;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.persistentDataPath;
            string relative = string.IsNullOrWhiteSpace(outputDirectory) ? "PCG_Metrics" : outputDirectory.Trim();
            return Path.Combine(projectRoot, relative);
        }

        private static void AppendRow(StringBuilder builder, IReadOnlyList<string> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append(Separator);
                builder.Append(Escape(values[i]));
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;
            bool mustQuote = value.Contains(Separator.ToString())
                             || value.Contains("\"")
                             || value.Contains("\n")
                             || value.Contains("\r");
            if (!mustQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static List<KeyValuePair<string, int>> SortCounts(Dictionary<string, int> counts)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (counts == null) return result;

            foreach (KeyValuePair<string, int> pair in counts)
                result.Add(pair);

            result.Sort((a, b) =>
            {
                int countCompare = b.Value.CompareTo(a.Value);
                return countCompare != 0
                    ? countCompare
                    : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            });

            return result;
        }

        private static string Int(int value)
        {
            return value.ToString(NumberCulture);
        }

        private static string Float(float value)
        {
            return value.ToString("0.###", NumberCulture);
        }

        private static string Double(double value)
        {
            return value.ToString("0.###", NumberCulture);
        }

        private static string Percent(double value)
        {
            return value.ToString("0.0%", NumberCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private sealed class AnalysisSummary
        {
            public int totalRuns;
            public int successfulRuns;
            public int failedRuns;
            public double successRate;
            public double emergencyFallbackRate;
            public double averageFailedAttempts;
            public double averageRoomsBeforeCapping;
            public double averageRoomsAfterCapping;
            public double roomTargetHitRate;
            public double averageOpenSocketsBeforeCapping;
            public double averageRemainingOpenSockets;
            public double averageBranchingFactor;
            public double averageCriticalPathRooms;
            public double averageSidePathRooms;
            public double averageBossDistance = -1d;
            public double averageConfiguredMinBossDistance;
            public double bossDistanceValidRate;
            public double specialRoomValidRate;
            public double averageTotalMs;
            public double averagePlacementMs;
            public double averageCappingMs;
            public double averageNavMeshMs;
            public double averageContentMs;
            public readonly Dictionary<string, int> variantTotals = new Dictionary<string, int>();
            public readonly Dictionary<string, int> errorCauseTotals = new Dictionary<string, int>();
            public readonly Dictionary<string, int> roomTypeTotals = new Dictionary<string, int>();
            public readonly Dictionary<string, int> generationFailureTotals = new Dictionary<string, int>();
            public readonly Dictionary<string, int> placementFailureTotals = new Dictionary<string, int>();
        }
    }
}
