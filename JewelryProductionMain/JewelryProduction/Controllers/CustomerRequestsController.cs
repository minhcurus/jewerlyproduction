﻿
using JewelryProduction.Common;
using JewelryProduction.DbContext;
using JewelryProduction.DTO;
using JewelryProduction.Interface;
using JewelryProduction.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace JewelryProduction.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerRequestsController : ControllerBase
    {
        private readonly JewelryProductionContext _context;
        private readonly ICustomerRequestService _requestService;

        public CustomerRequestsController(JewelryProductionContext context, ICustomerRequestService requestService)
        {
            _context = context;
            _requestService = requestService;
        }

        // GET: api/CustomerRequests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerRequest>>> GetCustomerRequests()
        {
            var result = await _requestService.GetCustomerRequests();
            return Ok(result);
        }

        // GET: api/CustomerRequests/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerRequest>> GetCustomerRequest(string id)
        {
            var result = await _requestService.GetCustomerRequest(id);
            return Ok(result);
        }

        // PUT: api/CustomerRequests/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomerRequest(string id, CustomerRequestDTO customerRequestDTO)
        {
            var primaryGemstone = await _context.Gemstones
                    .Where(g =>
                         customerRequestDTO.PrimaryGemstoneId.Contains(g.GemstoneId) &&
                        g.ProductSample == null && g.CustomizeRequestId == null)
                    .FirstOrDefaultAsync();

            if (primaryGemstone == null)
            {
                return BadRequest("The primary gemstone was not found.");
            }

            var additionalGemstones = await _context.Gemstones
                .Where(g => customerRequestDTO.AdditionalGemstone.Contains(g.Name) && g.CaratWeight >= 0.1 && g.CaratWeight <= 0.3)
                .GroupBy(g => g.Name)
                .Select(g => g.FirstOrDefault())
                .OrderBy(_ => Guid.NewGuid())
                .Take(2)
                .ToListAsync();
            var allSelectedGemstones = new List<Gemstone> { primaryGemstone }.Concat(additionalGemstones).ToList();
            var gold = await _context.Golds
            .FirstOrDefaultAsync(g => g.GoldType == customerRequestDTO.GoldType);

            if (gold == null)
            {
                return BadRequest("Gold type not found.");
            }
            var updateCusReq = await _context.CustomerRequests.FindAsync(id);
            updateCusReq.GoldId = gold.GoldId;
            updateCusReq.CustomerId = customerRequestDTO.CustomerId;
            updateCusReq.SaleStaffId = customerRequestDTO.SaleStaffId;
            updateCusReq.ManagerId = customerRequestDTO.ManagerId;
            updateCusReq.Type = customerRequestDTO.Type;
            updateCusReq.Style = customerRequestDTO.Style;
            updateCusReq.Size = customerRequestDTO.Size;
            updateCusReq.Quantity = customerRequestDTO.Quantity;
            updateCusReq.Gemstones = allSelectedGemstones;
            updateCusReq.Gold = gold;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerRequestExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }
        [HttpGet("customize-form-template")]
        public ActionResult<CustomerRequestDTO> GetCustomizeFormTemplate([FromQuery] string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                return BadRequest("Type must be provided.");
            }

            var template = new CustomerRequestDTO
            {
                Type = type
            };

            return Ok(template);
        }

        // POST: api/CustomerRequests
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<IActionResult> PostCustomerRequest([FromBody] CustomerRequestDTO customerRequestDTO)
        {
            try
            {
                var customerRequest = await _requestService.CreateCustomerRequestAsync(customerRequestDTO);
                return CreatedAtAction("GetCustomerRequest", new { id = customerRequest.CustomizeRequestId }, customerRequest);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE: api/CustomerRequests/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomerRequest(string id)
        {
            var customerRequest = await _context.CustomerRequests.FindAsync(id);
            if (customerRequest == null)
            {
                return NotFound();
            }

            _context.CustomerRequests.Remove(customerRequest);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CustomerRequestExists(string id)
        {
            return _context.CustomerRequests.Any(e => e.CustomizeRequestId == id);
        }
        [HttpGet("paging")]
        public async Task<IActionResult> GetAllPaging([FromQuery] OrderPagingRequest request)
        {
            var products = await _requestService.GetAllPaging(request);
            return Ok(products);
        }
        [HttpGet("prefill")]
        public async Task<IActionResult> PrefillCustomizeRequest([FromQuery] string productSampleId)
        {
            var productSample = await _context.ProductSamples
                .Where(ps => ps.ProductSampleId == productSampleId)
                .Select(ps => new
                {
                    Type = ps.Type,
                    Style = ps.Style,
                    GoldWeight = ps.GoldWeight,
                    Quantity = 1, // Default quantity, adjust as necessary
                    PrimaryGemstone = ps.Gemstones
                        .Where(g => g.CaratWeight > 0.3)
                        .Select(g => new AddGemstoneDTO
                        {
                            Name = g.Name,
                            Clarity = g.Clarity,
                            Color = g.Color,
                            Shape = g.Shape,
                            Size = g.Size,
                            Cut = g.Cut,
                            CaratWeight = g.CaratWeight,
                        }).FirstOrDefault(),
                    AdditionalGemstones = ps.Gemstones
                        .Where(g => g.CaratWeight <= 0.3)
                        .Select(g => new Gemstone
                        {
                            Name = g.Name,
                            Clarity = g.Clarity,
                            Color = g.Color,
                            Shape = g.Shape,
                            Size = g.Size,
                            Cut = g.Cut,
                            CaratWeight = g.CaratWeight,
                        })
                        .ToList(),
                    GoldType = ps.Gold.GoldType,
                })
                .FirstOrDefaultAsync();

            if (productSample == null)
            {
                return NotFound("Product sample not found.");
            }

            var customerRequest = new PrefillDTO
            {
                Type = productSample.Type,
                Style = productSample.Style,
                Quantity = productSample.Quantity,
                PrimaryGemstone = productSample.PrimaryGemstone,
                AdditionalGemstone = productSample.AdditionalGemstones.Select(g => g.Name).ToList(),
                GoldType = productSample.GoldType,
            };

            return Ok(customerRequest);
        }
        [HttpPost("approve/{customizeRequestId}")]
        public async Task<IActionResult> ApproveCustomerRequest(string customizeRequestId, string paymentMethodId)
        {
            var customerRequest = await _context.CustomerRequests
                .Include(cr => cr.Gemstones)
                .Include(cr => cr.Gold)
                .FirstOrDefaultAsync(cr => cr.CustomizeRequestId == customizeRequestId);

            if (customerRequest == null)
            {
                return NotFound("Customer request not found.");
            }

            if (customerRequest.quotation == null)
            {
                return BadRequest("Quotation is not available.");
            }

            customerRequest.Status = "Request Approved";

            var order = new Order
            {
                OrderId = await IdGenerator.GenerateUniqueId<Order>(_context, "ORD", 6),
                ProductionStaffId = null,
                DesignStaffId = null,
                OrderDate = DateTime.Now,
                DepositAmount = null,
                Status = "Choosing Payment",
                CustomizeRequestId = customerRequest.CustomizeRequestId,
                PaymentMethodId = paymentMethodId,
                TotalPrice = customerRequest.quotation.Value
            };

            _context.Orders.Add(order);

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    // Log the exception
                    Console.WriteLine($"Error: {ex.Message}");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
                }
            }

            return Ok(order);
        }
        [HttpPost("reject/{customizeRequestId}")]
        public async Task<IActionResult> RejectCustomerRequest(string customizeRequestId)
        {
            var customerRequest = await _context.CustomerRequests
                .Include(cr => cr.Gemstones)
                .FirstOrDefaultAsync(cr => cr.CustomizeRequestId == customizeRequestId);
            if (customerRequest == null)
            {
                return NotFound("Customer request not found.");
            }
            foreach (var gemstone in customerRequest.Gemstones)
            {
                gemstone.CustomizeRequestId = null;
            }
            customerRequest.Status = "Request Reject";
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return NoContent();
        }
        [HttpGet("{customerRequestId}/quotations")]
        public async Task<IActionResult> GetCustomerRequestQuotations(string customerRequestId)
        {
            var customerRequest = await _requestService.GetCustomerRequestWithQuotationsAsync(customerRequestId);

            if (customerRequest == null)
            {
                return NotFound();
            }

            var response = new
            {
                customerRequest.quotation,
                customerRequest.quotationDes
            };

            return Ok(response);
        }
        private string GetCurrentUserId()
        {
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sid);
            return userId;
        }
    }
}