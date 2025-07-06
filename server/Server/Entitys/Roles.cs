namespace Server.Entitys
{
    public class Roles
    {
        public const string Role_User = "User";
        public const string Role_Admin = "Admin";
        //public const string Role_Employee = "Employee";

        public const string StatusApproved = "Approved";
        public const string StatusCancelled = "Cancelled";
        public const string StatusRefunded = "Refunded";

        public const string PaymentStatusPending = "Pending";
        public const string PaymentStatusApproved = "Approved";
        public const string PaymentStatusDelayedPayment = "ApprovedForDelayedPayment";
        public const string PaymentStatusRejected = "Rejected";
    }
}
