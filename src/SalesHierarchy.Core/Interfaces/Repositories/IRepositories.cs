using System.Collections.Generic;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.Interfaces.Repositories
{
    public interface IPodStationRepository
    {
        IEnumerable<PodStation> GetAll();
        PodStation? GetById(int id);
        bool ExistsByOssId(int ossId, int? excludeId = null);
        int Add(PodStation station);
        bool Update(PodStation station);
        bool Delete(int id);
    }

    public interface ISalesPersonRepository
    {
        IEnumerable<SalesPerson> GetAll();
        SalesPerson? GetById(int id);
        IEnumerable<SalesPerson> GetDirectReports(int managerId);
        bool HasSubordinates(int salesPersonId);
        IReadOnlyList<int> GetPodIds(int salesPersonId);
        IReadOnlyList<SalesPersonPod> GetAllPodAssignments();
        bool AssignPod(int salesPersonId, int podId);
        bool RemovePod(int salesPersonId, int podId);
        bool ReplacePodOwners(int podId, IReadOnlyCollection<int> salesPersonIds);
        int Add(SalesPerson person);
        int AddWithPods(SalesPerson person, IReadOnlyCollection<int> podIds);
        bool Update(SalesPerson person);
        bool UpdateWithPods(SalesPerson person, IReadOnlyCollection<int> podIds);
        bool Delete(int id);
    }

    public interface IPodRepository
    {
        IEnumerable<Pod> GetAll();
        Pod? GetById(int id);
        IEnumerable<Pod> GetBySalesPersonId(int salespersonId);
        IEnumerable<Pod> GetByStationId(int stationId);
        IReadOnlyCollection<int> GetExistingIds(IEnumerable<int> podIds);
        bool ExistsByOssId(int ossId, int? excludeId = null);
        bool HasPodsForStation(int podStationId);
        bool HasPodsForSalesPerson(int salespersonId);
        int Add(Pod pod);
        bool Update(Pod pod);
        bool Delete(int id);
        void AssignPodsToStation(int stationId, IReadOnlyCollection<int> podIds);
        void ClearStationAssignments(int stationId);
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

    public interface IDealerMappingRepository
    {
        IEnumerable<DealerMapping> GetAll();
        IEnumerable<DealerMapping> GetBySalesPersonId(int salespersonId);
        DealerMapping? GetById(int id);
        bool Exists(int salespersonId, int dealerId, int? excludeId = null);
        bool HasMappingsForSalesPerson(int salespersonId);
        int Add(DealerMapping mapping);
        bool Update(DealerMapping mapping);
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
