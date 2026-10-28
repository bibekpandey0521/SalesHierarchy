using System.Collections.Generic;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.Interfaces.Repositories
{
    public interface IPODStationRepository
    {
        IEnumerable<PODStation> GetAll();
        PODStation? GetById(int id);
        bool ExistsByName(string name, int? excludeId = null);
        int Add(PODStation station);
        bool Update(PODStation station);
        bool Delete(int id);
    }

    public interface ISalesPersonRepository
    {
        IEnumerable<SalesPerson> GetAll();
        SalesPerson? GetById(int id);
        IEnumerable<SalesPerson> GetDirectReports(int managerId);
        bool HasSubordinates(int salesPersonId);
        int Add(SalesPerson person);
        bool Update(SalesPerson person);
        bool Delete(int id);
    }

    public interface IPODRepository
    {
        IEnumerable<POD> GetAll();
        POD? GetById(int id);
        IEnumerable<POD> GetBySalesPersonId(int salespersonId);
        bool HasPodsForStation(int podStationId);
        bool HasPodsForSalesPerson(int salespersonId);
        int Add(POD pod);
        bool Update(POD pod);
        bool Delete(int id);
    }

    public interface IDealerRepository
    {
        IEnumerable<Dealer> GetAll();
        Dealer? GetById(int id);
        bool ExistsByCode(string code, int? excludeId = null);
        bool HasMappings(int dealerId);
        int Add(Dealer dealer);
        bool Update(Dealer dealer);
        bool Delete(int id);
    }

    public interface ISalesDealerMappingRepository
    {
        IEnumerable<SalesDealerMapping> GetAll();
        IEnumerable<SalesDealerMapping> GetBySalesPersonId(int salespersonId);
        SalesDealerMapping? GetById(int id);
        bool Exists(int salespersonId, int dealerId, int? excludeId = null);
        bool HasMappingsForSalesPerson(int salespersonId);
        int Add(SalesDealerMapping mapping);
        bool Update(SalesDealerMapping mapping);
        bool Delete(int id);
    }

    public interface IAuditLogRepository
    {
        void Add(AuditLog log);
        IEnumerable<AuditLog> GetRecent(int take = 200, string? entityName = null, string? action = null, string? search = null);
        IEnumerable<AuditLog> GetForEntity(string entityName, string entityId, int take = 200);
        IEnumerable<AuditLog> GetForTeam(IReadOnlyCollection<int> salesPersonIds, IReadOnlyCollection<string> userEmails, int take = 300);
    }
}
