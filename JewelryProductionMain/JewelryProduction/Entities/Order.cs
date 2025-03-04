﻿using JewelryProduction.Entities;

namespace JewelryProduction;

public partial class Order
{
    public string OrderId { get; set; } = null!;

    public string? ProductionStaffId { get; set; } = null!;

    public string? DesignStaffId { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    public decimal? DepositAmount { get; set; }

    public string Status { get; set; } = null!;
    public string? Address { get; set; } = null!;

    public string? CustomizeRequestId { get; set; }

    public string PaymentMethodId { get; set; } = null!;

    public decimal TotalPrice { get; set; }

    public CustomerRequest? CustomizeRequest { get; set; }

    public Insurance? Insurance { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = null!;

    public AppUser? ProductionStaff { get; set; } 
    public AppUser? DesignStaff { get; set; } 
    public ICollection<_3ddesign> _3ddesigns { get; set; } = new List<_3ddesign>();
    public ICollection<Inspection> Inspections { get; set; } = new List<Inspection>();
}
