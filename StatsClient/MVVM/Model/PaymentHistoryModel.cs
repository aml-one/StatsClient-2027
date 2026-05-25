using System;

namespace StatsClient.MVVM.Model
{
    public class PaymentHistoryModel
    {
        public int Id { get; set; }
        public string PaymentID { get; set; }
        public string OrderID { get; set; }
        public string DesignerID { get; set; }
        public string FriendlyName { get; set; }
        public string ImportPath { get; set; }
        public string DateTime { get; set; }
        public string ImportTime { get; set; }
        public string IsitRedo { get; set; }
        public string Crowns { get; set; }
        public string Gingiva { get; set; }
        public string Abutments { get; set; }
        public string TotalUnits { get; set; }
        public string LxLabnextID { get; set; }
        public string LxCreationDate { get; set; }
        public string LxInvoiceDate { get; set; }
        public int? LxPanNumber { get; set; }
        public string LxStatus { get; set; }
        public string LxPatient_FirstName { get; set; }
        public string LxPatient_LastName { get; set; }
        public int? LxUnitCount { get; set; }
        public string LxItems { get; set; }
        public string LxTeethNumbers { get; set; }
        public int? LxPrice { get; set; }
        public short? LxPaid { get; set; }
        public string LxIssue { get; set; }
        public string LxInvoiceDateRange { get; set; }
        public string ProcessedBy { get; set; }
        public short? IsAutoProcess { get; set; }
        public string Customer { get; set; }
        public string OrderID2 { get; set; }
        public string PayPeriod { get; set; }

        // Computed property for full patient name
        public string PatientFullName => $"{LxPatient_FirstName} {LxPatient_LastName}".Trim();

        // Computed property for formatted date and time (e.g., "Feb 4, 2026 4:12 PM")
        public string FormattedDateTime
        {
            get
            {
                if (System.DateTime.TryParse(DateTime, out System.DateTime dt))
                {
                    return dt.ToString("MMM d, yyyy h:mm tt");
                }
                return DateTime ?? "";
            }
        }

        // Computed properties for individual unit displays
        public string CrownsDisplay => Crowns ?? "0";
        public string AbutmentsDisplay => Abutments ?? "0";

        // Computed property for total units count
        public int TotalUnitsCount
        {
            get
            {
                int crowns = 0;
                int abutments = 0;
                int.TryParse(Crowns, out crowns);
                int.TryParse(Abutments, out abutments);
                return crowns + abutments;
            }
        }

        // Computed property for units display (for backward compatibility)
        public string UnitsDisplay => $"{CrownsDisplay} Cr / {AbutmentsDisplay} Ab";

        // Computed property for paid status
        public bool IsPaid => LxPaid == 1;

        // Computed property for not paid status (inverse of IsPaid)
        public bool IsNotPaid => !IsPaid;
    }
}
