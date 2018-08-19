using System;

namespace ReadOnlyDetector
{
    public class BorderlandsDateFormat
    {
        #region Fields
        // The year, formatted like BL2
        public string Year { get; }

        // The Month formatted like BL2
        public string Month { get; }

        // The day formatted like BL2
        public string Day { get; }

        // The hours formatted like BL2
        public string Hours { get; }

        // The minutes formatted like BL2
        public string Minutes { get; }

        // The seconds formatted like BL2
        public string Seconds { get; }

        // The postfix of "PM" or "AM"
        public string PMorAM { get; }
        #endregion

        #region Constructors
        public BorderlandsDateFormat()
        {
            DateTime today = DateTime.Today;
            Year = today.Year.ToString();
            Month = today.Month.ToString();
            Day = today.Day.ToString();
            Hours = today.Hour.ToString();
            Minutes = today.Minute.ToString();
            Seconds = today.Second.ToString();
            PMorAM = today.ToString("tt");
        }

        public BorderlandsDateFormat(long year, long month, long day, long hours, long minutes, long seconds, string PM)
        {
            Year = Year.ToString();
            Month = Month.ToString();
            Day = Day.ToString();
            Hours = hours.ToString();
            Minutes = minutes.ToString();
            Seconds = seconds.ToString();
            PMorAM = PM;
        }

        public BorderlandsDateFormat(string years, string months, string days, string hours, string minutes,
            string seconds, string PM)
        {
            Year = years;
            Month = months;
            Day = days;
            Hours = hours;
            Minutes = minutes;
            Seconds = seconds;
            PMorAM = PM;
        }
        #endregion

        #region Methods

        public string getDateFormat()
        {
            string value = Year.ToString() +
                           fixDate(Month).ToString() +
                           fixDate(Day).ToString() +
                           fixDate(Hours).ToString() +
                           fixDate(Minutes).ToString() +
                           fixDate(Seconds).ToString();
            return value;

        }

        // This fixes the improper formatting of the values
        private static string fixDate(string dateToFix)
        {
            if (10 > Convert.ToInt64(dateToFix))
            {
                dateToFix = "0" + dateToFix;
            }
            if (Convert.ToInt64(dateToFix) == 0)
            {
                dateToFix = "00";
            }
            return dateToFix.ToString();
        }

        // This returns a normally formatted date format like, "mm/dd/yy hh:mm:ss tt"
        public override string ToString()
        {
            return Month + "/" + Day + "/" + Year + " " + Hours + ":" + Minutes + ":" + Seconds + " " + PMorAM;
        }
        #endregion
    }
}
