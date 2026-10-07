using Dapper;
using Oracle.ManagedDataAccess.Client;
using RequestPortal.Core.Abstractions;
using RequestPortal.Core.Models;

namespace RequestPortal.Data.Repositories;

/// <summary>Dapper repo for the RequestType↔UnitType and Unit↔Vertical interlink mappings.</summary>
public sealed class ClassificationMapRepo : IClassificationMapRepo
{
    private readonly IDbConnectionFactory _factory;
    public ClassificationMapRepo(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<UnitType>> GetUnitTypesForRequestTypeAsync(long requestTypeId, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = @"SELECT ut.ID AS ""Id"", ut.CODE AS ""Code"", ut.NAME AS ""Name"", ut.IS_ACTIVE AS ""IsActive""
                    FROM RP_M_REQTYPE_UNITTYPE m JOIN RP_M_UNIT_TYPE ut ON ut.ID = m.UNIT_TYPE_ID
                    WHERE m.REQUEST_TYPE_ID = :rt AND ut.IS_ACTIVE = 1 ORDER BY ut.NAME";
        return (await c.QueryAsync<UnitType>(new CommandDefinition(sql, new { rt = requestTypeId }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<Vertical>> GetVerticalsForUnitAsync(long unitId, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = @"SELECT v.ID AS ""Id"", v.CODE AS ""Code"", v.NAME AS ""Name"", v.IS_ACTIVE AS ""IsActive""
                    FROM RP_M_UNIT_VERTICAL m JOIN RP_M_VERTICAL v ON v.ID = m.VERTICAL_ID
                    WHERE m.UNIT_ID = :u AND v.IS_ACTIVE = 1 ORDER BY v.NAME";
        return (await c.QueryAsync<Vertical>(new CommandDefinition(sql, new { u = unitId }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<long>> GetMappedUnitTypeIdsAsync(long requestTypeId, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = "SELECT UNIT_TYPE_ID FROM RP_M_REQTYPE_UNITTYPE WHERE REQUEST_TYPE_ID = :rt";
        return (await c.QueryAsync<long>(new CommandDefinition(sql, new { rt = requestTypeId }, cancellationToken: ct))).AsList();
    }

    public async Task<IReadOnlyList<long>> GetMappedVerticalIdsAsync(long unitId, CancellationToken ct = default)
    {
        using var c = await _factory.OpenAsync(ct);
        var sql = "SELECT VERTICAL_ID FROM RP_M_UNIT_VERTICAL WHERE UNIT_ID = :u";
        return (await c.QueryAsync<long>(new CommandDefinition(sql, new { u = unitId }, cancellationToken: ct))).AsList();
    }

    public async Task SetRequestTypeUnitTypesAsync(long requestTypeId, IEnumerable<long> unitTypeIds, CancellationToken ct = default)
    {
        using var c = (OracleConnection)await _factory.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RP_M_REQTYPE_UNITTYPE WHERE REQUEST_TYPE_ID = :rt", new { rt = requestTypeId }, cancellationToken: ct));
        foreach (var ut in unitTypeIds.Distinct())
            await c.ExecuteAsync(new CommandDefinition(
                "INSERT INTO RP_M_REQTYPE_UNITTYPE (ID,REQUEST_TYPE_ID,UNIT_TYPE_ID) VALUES (RP_MAP_SEQ.NEXTVAL,:rt,:ut)",
                new { rt = requestTypeId, ut }, cancellationToken: ct));
    }

    public async Task SetUnitVerticalsAsync(long unitId, IEnumerable<long> verticalIds, CancellationToken ct = default)
    {
        using var c = (OracleConnection)await _factory.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(
            "DELETE FROM RP_M_UNIT_VERTICAL WHERE UNIT_ID = :u", new { u = unitId }, cancellationToken: ct));
        foreach (var v in verticalIds.Distinct())
            await c.ExecuteAsync(new CommandDefinition(
                "INSERT INTO RP_M_UNIT_VERTICAL (ID,UNIT_ID,VERTICAL_ID) VALUES (RP_MAP_SEQ.NEXTVAL,:u,:v)",
                new { u = unitId, v }, cancellationToken: ct));
    }
}
