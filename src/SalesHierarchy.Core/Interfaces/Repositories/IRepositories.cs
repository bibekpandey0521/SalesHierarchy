using System.Collections.Generic;
using SalesHierarchy.Core.Models;

namespace SalesHierarchy.Core.Interfaces.Repositories
{
    public interface IPODStationRepository
    {
        IEnumerable<PODStation> GetAll();
        PODStation? GetById(int id);
        int Add(PODStation station);
        bool Update(PODStation station);
        bool Delete(int id);
    }

    public interface ISalesPersonRepository
    {
        IEnumerable<SalesPerson> GetAll();
        SalesPerson? GetById(int id);
        int Add(SalesPerson person);
        bool Update(SalesPerson person);
        bool Delete(int id);
        IEnumerable<SalesPerson> GetDirectReports(int managerId);
    }

    public interface IPODRepository
    {
        IEnumerable<POD> GetAll();
        POD? GetById(int id);
        IEnumerable<POD> GetBySalesPersonId(int salespersonId);
        int Add(POD pod);
        bool Update(POD pod);
        bool Delete(int id);
    }

    public interface IDealerRepository
    {
        IEnumerable<Dealer> GetAll();
        Dealer? GetById(int id);
        int Add(Dealer dealer);
        bool Update(Dealer dealer);
        bool Delete(int id);
    }

    public interface ISalesDealerMappingRepository
    {
        IEnumerable<SalesDealerMapping> GetAll();
        IEnumerable<SalesDealerMapping> GetBySalesPersonId(int salespersonId);
        SalesDealerMapping? GetById(int id);
        int Add(SalesDealerMapping mapping);
        bool Delete(int id);
        bool Exists(int salespersonId, int dealerId);
    }
}
