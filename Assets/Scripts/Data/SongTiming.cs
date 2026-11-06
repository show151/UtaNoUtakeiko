using System;

namespace UtaNoUtakeiko.Data
{
    public static class SongTiming
    {
        public static double StartBeat(SongChart chart, int measure, double beatInMeasure)
        {
            double beatsPerMeasure = chart.TimeSignatureNumerator * 4.0 / chart.TimeSignatureDenominator;
            if (measure < 1 || beatsPerMeasure <= 0 || chart.PickupBeats < 0 || chart.PickupBeats >= beatsPerMeasure)
                throw new ArgumentException("Invalid measure or pickup length.");
            double omitted = chart.PickupBeats > 0 && measure > 1 ? beatsPerMeasure - chart.PickupBeats : 0;
            return (measure - 1) * beatsPerMeasure + beatInMeasure - omitted;
        }

        public static double TotalBeats(SongChart chart, int lastMeasure)
        {
            double beatsPerMeasure = chart.TimeSignatureNumerator * 4.0 / chart.TimeSignatureDenominator;
            return StartBeat(chart, lastMeasure, 0) + (lastMeasure == 1 && chart.PickupBeats > 0 ? chart.PickupBeats : beatsPerMeasure);
        }
    }
}
