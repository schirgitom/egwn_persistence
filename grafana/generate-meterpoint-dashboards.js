const fs = require('node:fs');
const path = require('node:path');

const timescale = 'bfywptosighs0b';
const centralApi = 'bfz0tx8cvxszkc';
const datasource = name => ({ name });
const query = (sql, format = 'table', refId = 'A') => ({
  kind: 'PanelQuery',
  spec: {
    hidden: false,
    refId,
    query: {
      kind: 'DataQuery',
      group: 'grafana-postgresql-datasource',
      version: 'v0',
      datasource: datasource(timescale),
      spec: { editorMode: 'code', format, rawQuery: true, rawSql: sql }
    }
  }
});

const thresholds = (colors = ['green', 'red']) => ({
  mode: 'absolute',
  steps: colors.map((color, i) => ({ color, value: i ? 80 : 0 }))
});
const color = fixedColor => ({ mode: 'fixed', fixedColor });
const override = (name, fixedColor) => ({
  matcher: { id: 'byName', options: name },
  properties: [{ id: 'color', value: color(fixedColor) }]
});
const statOptions = calc => ({
  colorMode: 'value',
  graphMode: 'none',
  reduceOptions: { calcs: [calc], fields: '', values: false }
});
const timeSeriesOptions = {
  legend: { displayMode: 'list', placement: 'bottom', showLegend: true },
  tooltip: { mode: 'multi', sort: 'desc' }
};
const timeSeriesDefaults = {
  unit: 'kwatth',
  custom: {
    drawStyle: 'line', fillOpacity: 12, lineWidth: 2,
    showPoints: 'auto', spanNulls: false,
    stacking: { group: 'A', mode: 'none' }
  }
};
const barDefaults = {
  unit: 'kwatt',
  custom: { fillOpacity: 85, axisLabel: 'kW' }
};

function dashboard(direction) {
  const generation = direction === 'generation';
  const name = generation ? 'einspeisung' : 'bezug';
  const total = generation ? 'Erzeugung' : 'Verbrauch';
  const grid = generation ? 'Netzeinspeisung' : 'Netzbezug';
  const community = generation ? 'Abgabe an EG' : 'Bezug aus EG';
  const ratio = generation ? 'EG-Abgabequote' : 'EG-Deckungsgrad';
  const gridColor = generation ? 'semi-dark-yellow' : 'red';
  const elements = {};
  const items = [];
  function add(id, title, type, sql, format, x, y, width, height, defaults = {}, overrides = [], options = {}, description = '') {
    elements[`panel-${id}`] = {
      kind: 'Panel',
      spec: {
        id, title, description, links: [],
        data: {
          kind: 'QueryGroup',
          spec: {
            queries: Array.isArray(sql) ? sql.map((statement, index) => query(statement, format, String.fromCharCode(65 + index))) : [query(sql, format)],
            queryOptions: {}, transformations: []
          }
        },
        vizConfig: {
          kind: 'VizConfig', group: type, version: '13.2.2',
          spec: { fieldConfig: { defaults, overrides }, options }
        }
      }
    };
    items.push({
      kind: 'GridLayoutItem',
      spec: { element: { kind: 'ElementReference', name: `panel-${id}` }, x, y, width, height }
    });
  }

  // Both sources use the same external meter number; only the measurement DB has the community-scoped energy.
  const filtered = `FROM raw_measurements r
JOIN meter_points mp ON mp."Id" = r.meter_id
WHERE $__timeFilter(r.timestamp)
  AND r.community_id = \${community:sqlstring}
  AND mp.meter_point_number IN (\${meterPoint:sqlstring})`;
  const stats = [
    [1, total, 'total_consumption', 'purple'],
    [2, grid, 'grid_share', gridColor],
    [3, community, 'community_share', 'green']
  ];
  for (const [id, title, column, shade] of stats) {
    add(id, title, 'stat', `SELECT SUM(r.${column}) AS "${title}"\n${filtered}`, 'table',
      (id - 1) * 4, 0, 4, 4, { unit: 'kwatth', decimals: 1, color: color(shade) }, [], statOptions('lastNotNull'));
  }
  add(4, ratio, 'stat',
    `SELECT SUM(r.community_share) / NULLIF(SUM(r.total_consumption), 0) AS "${ratio}"\n${filtered}`,
    'table', 12, 0, 4, 4,
    { unit: 'percentunit', decimals: 1, min: 0, max: 1, color: { mode: 'thresholds' },
      thresholds: { mode: 'absolute', steps: [
        { color: 'red', value: 0 }, { color: 'orange', value: 0.2 }, { color: 'green', value: 0.6 }
      ] } }, [], statOptions('lastNotNull'));
  add(13, generation ? 'Spitzenleistung' : 'Spitzenlast', 'stat',
    `SELECT MAX(power_kw) AS "Spitzenleistung"
FROM (SELECT r.timestamp, SUM(r.total_consumption) AS power_kw
      ${filtered} GROUP BY r.timestamp) hours`,
    'table', 16, 0, 4, 4, { unit: 'kwatt', decimals: 2, color: color('orange') },
    [], statOptions('lastNotNull'), 'Höchste gemeinsame Stundenleistung (stündliche kWh entsprechen mittleren kW).');
  add(14, generation ? 'Anteil an allen Einspeisern' : 'Anteil an allen Bezügern', 'stat',
    `SELECT SUM(r.total_consumption) FILTER (WHERE mp.meter_point_number IN (\${meterPoint:sqlstring}))
       / NULLIF(SUM(r.total_consumption), 0) AS "Anteil"
FROM raw_measurements r
JOIN meter_points mp ON mp."Id" = r.meter_id
WHERE $__timeFilter(r.timestamp)
  AND r.community_id = \${community:sqlstring}
  AND mp.meter_point_number IN (\${allMeters:sqlstring})`,
    'table', 20, 0, 4, 4,
    { unit: 'percentunit', decimals: 1, min: 0, max: 1, color: color('blue') },
    [], statOptions('lastNotNull'), 'Anteil der gewählten Zählpunkte an den aktiven Zählpunkten dieser Richtung in der gewählten Gemeinschaft.');

  add(6, `${total} – ${grid} vs. ${community}`, 'timeseries',
    `SELECT $__timeGroupAlias(r.timestamp, $__interval),
       SUM(r.grid_share) AS "${grid}", SUM(r.community_share) AS "${community}"
${filtered} GROUP BY 1 ORDER BY 1`,
    'time_series', 0, 4, 12, 9,
    { ...timeSeriesDefaults, custom: { ...timeSeriesDefaults.custom, drawStyle: 'bars',
      fillOpacity: 90, lineWidth: 0, stacking: { group: 'A', mode: 'normal' } } },
    [override(grid, gridColor), override(community, 'green')], timeSeriesOptions,
    'kWh je Intervall; fehlende Stunden bleiben Lücken. Bei langen Zeiträumen werden die Daten gruppiert.');
  add(7, `${ratio} im Verlauf`, 'timeseries',
    `SELECT $__timeGroupAlias(r.timestamp, $__interval),
       SUM(r.community_share) / NULLIF(SUM(r.total_consumption), 0) AS "${ratio}"
${filtered} GROUP BY 1 ORDER BY 1`,
    'time_series', 12, 4, 12, 9,
    { ...timeSeriesDefaults, unit: 'percentunit', min: 0, max: 1, color: color('green') },
    [], timeSeriesOptions);
  add(16, 'Leistung – Mittel und Maximum je Intervall', 'timeseries',
    `WITH hours AS (
  SELECT r.timestamp, SUM(r.total_consumption) AS power_kw
  ${filtered} GROUP BY r.timestamp
)
SELECT $__timeGroupAlias(timestamp, $__interval),
       AVG(power_kw) AS "Ø Leistung", MAX(power_kw) AS "Max. Leistung"
FROM hours GROUP BY 1 ORDER BY 1`,
    'time_series', 0, 13, 12, 9,
    { ...timeSeriesDefaults, unit: 'kwatt' },
    [override('Ø Leistung', 'blue'), override('Max. Leistung', 'orange')], timeSeriesOptions,
    'Durchschnitt und Maximum der gemeinsamen stündlichen Leistung; keine Viertelstundenwerte.');
  add(17, 'Tägliche Spitzen- und Durchschnittsleistung', 'timeseries',
    `WITH hours AS (
  SELECT r.timestamp, SUM(r.total_consumption) AS power_kw
  ${filtered} GROUP BY r.timestamp
)
SELECT (date_trunc('day', timestamp AT TIME ZONE 'Europe/Vienna') AT TIME ZONE 'Europe/Vienna') AS "time",
       MAX(power_kw) AS "Spitzenleistung", AVG(power_kw) AS "Ø Leistung"
FROM hours GROUP BY 1 ORDER BY 1`,
    'time_series', 12, 13, 12, 9,
    { ...timeSeriesDefaults, unit: 'kwatt' },
    [override('Spitzenleistung', 'orange'), override('Ø Leistung', 'blue')], timeSeriesOptions);
  add(10, `Ø Stundenprofil – ${grid} vs. ${community}`, 'barchart',
    `WITH hours AS (
  SELECT r.timestamp, SUM(r.grid_share) AS grid_kw, SUM(r.community_share) AS community_kw
  ${filtered} GROUP BY r.timestamp
)
SELECT to_char(timestamp AT TIME ZONE 'Europe/Vienna', 'HH24:00') AS "Uhrzeit",
       AVG(grid_kw) AS "${grid}", AVG(community_kw) AS "${community}"
FROM hours GROUP BY 1 ORDER BY 1`,
    'table', 0, 22, 12, 9, barDefaults,
    [override(grid, gridColor), override(community, 'green')],
    { ...timeSeriesOptions, xField: 'Uhrzeit', orientation: 'vertical', stacking: 'normal' },
    'Mittlere stündliche Leistung nach Wiener Ortszeit; die doppelte Stunde bei Winterzeit wird gemittelt.');
  add(18, generation ? 'EG-Abgabequote nach Tageszeit' : 'Ø Stundenprofil – Werktag vs. Wochenende',
    'barchart', generation
      ? `SELECT to_char(r.timestamp AT TIME ZONE 'Europe/Vienna', 'HH24:00') AS "Uhrzeit",
       SUM(r.community_share) / NULLIF(SUM(r.total_consumption), 0) AS "${ratio}"
${filtered} GROUP BY 1 ORDER BY 1`
      : `WITH hours AS (
  SELECT r.timestamp, SUM(r.total_consumption) AS power_kw
  ${filtered} GROUP BY r.timestamp
)
SELECT to_char(timestamp AT TIME ZONE 'Europe/Vienna', 'HH24:00') AS "Uhrzeit",
       AVG(power_kw) FILTER (WHERE EXTRACT(ISODOW FROM timestamp AT TIME ZONE 'Europe/Vienna') < 6) AS "Werktag",
       AVG(power_kw) FILTER (WHERE EXTRACT(ISODOW FROM timestamp AT TIME ZONE 'Europe/Vienna') >= 6) AS "Wochenende"
FROM hours GROUP BY 1 ORDER BY 1`,
    'table', 12, 22, 12, 9,
    generation ? { ...barDefaults, unit: 'percentunit', min: 0, max: 1 } : barDefaults,
    generation ? [override(ratio, 'green')] : [override('Werktag', 'blue'), override('Wochenende', 'orange')],
    { ...timeSeriesOptions, xField: 'Uhrzeit', orientation: 'vertical', stacking: 'none' },
    generation ? 'Abgabe an die EG / Erzeugung je Wiener Ortsstunde (mengengewichtet).' : 'Feiertage zählen als Werktage.');
  add(19, generation ? 'Erzeugungsdauerlinie' : 'Lastdauerlinie', 'xychart',
    `WITH hours AS (
  SELECT r.timestamp, SUM(r.total_consumption) AS power_kw
  ${filtered} GROUP BY r.timestamp
)
SELECT ROW_NUMBER() OVER (ORDER BY power_kw DESC) AS "Stunden",
       power_kw AS "Leistung"
FROM hours ORDER BY 1`,
    'table', 0, 42, 12, 9, { unit: 'kwatt', color: color('orange'),
      custom: { show: 'lines', lineWidth: 2 } }, [],
    { mapping: 'auto', series: [{}] },
    'Absteigende Stundenleistung; X zählt tatsächlich vorhandene Stunden.');
  add(20, `Vergleich mit Vorwoche (−7 Tage)`, 'timeseries',
    [
      `SELECT $__timeGroupAlias(r.timestamp, $__interval), SUM(r.total_consumption) AS "Aktuell"
${filtered} GROUP BY 1 ORDER BY 1`,
      `SELECT $__timeGroupAlias(r.timestamp + INTERVAL '7 days', $__interval),
       SUM(r.total_consumption) AS "Vorwoche"
FROM raw_measurements r JOIN meter_points mp ON mp."Id" = r.meter_id
WHERE r.timestamp >= $__timeFrom()::timestamptz - INTERVAL '7 days'
  AND r.timestamp < $__timeTo()::timestamptz - INTERVAL '7 days'
  AND r.community_id = \${community:sqlstring}
  AND mp.meter_point_number IN (\${meterPoint:sqlstring})
GROUP BY 1 ORDER BY 1`
    ], 'time_series', 12, 42, 12, 9, timeSeriesDefaults,
    [override('Aktuell', 'blue'), override('Vorwoche', 'orange')], timeSeriesOptions,
    'Stündliche Rohwerte; verschiebt die Vorwoche um exakt sieben Tage.');

  const quality = `SELECT mp.meter_point_number AS "Zählpunkt",
       COUNT(r.timestamp) AS "Datensätze",
       COUNT(r.timestamp)::numeric / NULLIF((
         SELECT COUNT(*) FROM generate_series(
           date_trunc('hour', $__timeFrom()::timestamptz),
           LEAST(NOW(), $__timeTo()::timestamptz),
           INTERVAL '1 hour') slot
         WHERE slot >= $__timeFrom()::timestamptz
           AND slot < LEAST(NOW(), $__timeTo()::timestamptz)
       ), 0) AS "Vollständigkeit",
       COUNT(*) FILTER (WHERE r.total_consumption = 0) AS "Nullwerte",
       COUNT(*) FILTER (WHERE ABS(r.grid_share + r.community_share - r.total_consumption) > 0.001) AS "Inkonsistent",
       SUM(r.total_consumption) AS "${total}",
       SUM(r.community_share) / NULLIF(SUM(r.total_consumption), 0) AS "${ratio}",
       MAX(r.timestamp) AS "Letzter Datensatz",
       EXTRACT(EPOCH FROM (NOW() - MAX(r.timestamp))) / 3600 AS "Alter (h)"
FROM meter_points mp
JOIN meter_point_communities mc ON mc.meter_id = mp."Id"
  AND mc.community_id = \${community:sqlstring}
LEFT JOIN raw_measurements r ON r.meter_id = mp."Id"
  AND r.community_id = \${community:sqlstring} AND $__timeFilter(r.timestamp)
WHERE mp.meter_point_number IN (\${meterPoint:sqlstring})
GROUP BY mp.meter_point_number ORDER BY "Alter (h)" DESC NULLS FIRST`;
  add(12, 'Datenqualität je Zählpunkt', 'table', quality, 'table',
    0, 51, 15, 9, {}, [
      { matcher: { id: 'byName', options: 'Vollständigkeit' },
        properties: [{ id: 'unit', value: 'percentunit' }] },
      { matcher: { id: 'byName', options: ratio },
        properties: [{ id: 'unit', value: 'percentunit' }] },
      { matcher: { id: 'byName', options: total },
        properties: [{ id: 'unit', value: 'kwatth' }] }
    ], { showHeader: true },
    'Erwartet: UTC-Stunden im gewählten Zeitraum bis jetzt (Sommer-/Winterzeit korrekt). Bei Einspeisern sind Nullwerte nachts normal.');
  add(22, generation ? 'Top 10 Einspeisespitzen' : 'Top 10 Lastspitzen', 'table',
    `SELECT r.timestamp AS "Zeitpunkt",
       SUM(r.total_consumption) AS "Leistung",
       SUM(r.community_share) / NULLIF(SUM(r.total_consumption), 0) AS "${ratio}"
${filtered} GROUP BY r.timestamp ORDER BY 2 DESC LIMIT 10`,
    'table', 15, 51, 9, 9, {}, [
      { matcher: { id: 'byName', options: 'Leistung' },
        properties: [{ id: 'unit', value: 'kwatt' }] },
      { matcher: { id: 'byName', options: ratio },
        properties: [{ id: 'unit', value: 'percentunit' }] }
    ], { showHeader: true }, 'Stunden mit der höchsten gemeinsamen Leistung.');

  const days = `FROM generate_series(
  date_trunc('day', $__timeFrom()::timestamptz AT TIME ZONE 'Europe/Vienna'),
  date_trunc('day', $__timeTo()::timestamptz AT TIME ZONE 'Europe/Vienna'),
  INTERVAL '1 day') AS d(local_day)`;
  add(11, 'Stündliche Leistung nach Tageszeit', 'status-history',
    `WITH hours AS (
  SELECT (r.timestamp AT TIME ZONE 'Europe/Vienna')::date AS local_date,
         EXTRACT(HOUR FROM r.timestamp AT TIME ZONE 'Europe/Vienna')::int AS local_hour,
         SUM(r.total_consumption) AS power_kw
  ${filtered} GROUP BY 1, 2
)
SELECT (d.local_day AT TIME ZONE 'Europe/Vienna') AS "time",
       to_char(TIME '00:00' + n * INTERVAL '1 hour', 'HH24:00') AS metric,
       h.power_kw AS value
${days}
CROSS JOIN generate_series(0, 23) AS n
LEFT JOIN hours h ON h.local_date = d.local_day::date AND h.local_hour = n
ORDER BY 1, 2`,
    'time_series', 0, 31, 24, 11,
    { unit: 'kwatt', color: { mode: 'continuous-GrYlRd' }, decimals: 2 },
    [], { showValue: 'never', rowHeight: 0.9, colWidth: 0.9,
      legend: { showLegend: true, displayMode: 'list', placement: 'bottom' } },
    'Jede Zeile eine Wiener Ortsstunde, jede Spalte ein Tag. Leere Zellen sind fehlende Messwerte; doppelte Winterzeit-Stunden werden summiert.');
  add(21, 'Datenvollständigkeit je Tag', 'status-history',
    `SELECT (d.local_day AT TIME ZONE 'Europe/Vienna') AS "time",
       mp.meter_point_number AS metric,
       COUNT(r.timestamp)::numeric / NULLIF((
         SELECT COUNT(*) FROM generate_series(
           d.local_day AT TIME ZONE 'Europe/Vienna',
           (d.local_day + INTERVAL '1 day') AT TIME ZONE 'Europe/Vienna',
           INTERVAL '1 hour') slot
         WHERE slot >= $__timeFrom()::timestamptz
           AND slot < LEAST(NOW(), $__timeTo()::timestamptz)
           AND slot < (d.local_day + INTERVAL '1 day') AT TIME ZONE 'Europe/Vienna'
       ), 0) AS value
${days}
CROSS JOIN meter_points mp
JOIN meter_point_communities mc ON mc.meter_id = mp."Id"
  AND mc.community_id = \${community:sqlstring}
LEFT JOIN raw_measurements r ON r.meter_id = mp."Id"
  AND r.community_id = \${community:sqlstring}
  AND r.timestamp >= (d.local_day AT TIME ZONE 'Europe/Vienna')
  AND r.timestamp < ((d.local_day + INTERVAL '1 day') AT TIME ZONE 'Europe/Vienna')
  AND $__timeFilter(r.timestamp)
WHERE mp.meter_point_number IN (\${meterPoint:sqlstring})
GROUP BY d.local_day, mp.meter_point_number ORDER BY 1, 2`,
    'time_series', 0, 60, 24, 7,
    { unit: 'percentunit', decimals: 0, min: 0, max: 1,
      color: { mode: 'thresholds' },
      thresholds: { mode: 'absolute', steps: [
        { color: 'red', value: 0 }, { color: 'orange', value: 0.9 },
        { color: 'green', value: 0.99 }
      ] } }, [], { showValue: 'never', rowHeight: 0.9, colWidth: 0.9,
      legend: { showLegend: false } },
    'Gelieferte / erwartete Stunden je Wiener Kalendertag; 23/25 Stunden bei Zeitumstellung, nur bereits vergangene Stunden am aktuellen Tag.');

  const centralSql = `SELECT COALESCE(
         (SELECT string_agg(DISTINCT c."Name", ', ' ORDER BY c."Name")
          FROM "CustomerMeteringPoints" cm
          JOIN "Customers" c ON c."Id" = cm."CustomerId" AND NOT c."IsDeleted"
          WHERE cm."MeteringPointId" = mp."Id"), 'Ohne Kunde')
       || COALESCE(' – ' || NULLIF(mp."Description", ''), '')
       || ' (…' || RIGHT(mp."MeteringPointCode", 6) || ')' AS __text,
       mp."MeteringPointCode" AS __value
FROM "MeteringPoints" mp
WHERE NOT mp."IsDeleted" AND lower(mp."EnergyDirection") = '${direction}'
  AND mp."MeteringPointCode" IN (\${communityMeters:sqlstring})
ORDER BY 1, 2`;
  const variable = (variableName, label, sql, source, hidden, multi = false, includeAll = false) => ({
    kind: 'QueryVariable',
    spec: {
      name: variableName, label, hide: hidden ? 'hideVariable' : 'dontHide',
      multi, includeAll, allowCustomValue: false,
      current: includeAll ? { text: ['All'], value: ['$__all'] } : { text: '', value: '' },
      options: [], refresh: 'onDashboardLoad', sort: 'disabled', regex: '',
      regexApplyTo: 'value', skipUrlSync: hidden,
      definition: '',
      query: {
        kind: 'DataQuery', group: 'grafana-postgresql-datasource', version: 'v0',
        datasource: datasource(source),
        spec: { editorMode: 'code', format: 'table', rawSql: sql, refId: 'SQLVariableQueryEditor-VariableQuery' }
      }
    }
  });
  return {
    apiVersion: 'dashboard.grafana.app/v2', kind: 'Dashboard',
    metadata: { name: `egwn-meterpoints-${name}` },
    spec: {
      title: `EGWN – Zählpunkte ${generation ? 'Einspeisung' : 'Bezug'}`,
      description: 'Stündliche, gemeinschaftsspezifische Messwerte aus EGWNPersistence; aktive Zählpunkte aus EGWNCentralAPI.',
      editable: true, cursorSync: 'Crosshair',
      annotations: [], links: [], liveNow: false, preload: false,
      elements, layout: { kind: 'GridLayout', spec: { items } },
      tags: ['egwn', 'meterpoints', name],
      timeSettings: { from: 'now-7d', to: 'now', timezone: 'Europe/Vienna',
        autoRefresh: '5m', autoRefreshIntervals: ['1m', '5m', '15m', '1h', '1d'],
        weekStart: 'monday', fiscalYearStartMonth: 0, hideTimepicker: false },
      variables: [
        variable('community', 'Energiegemeinschaft',
          `SELECT ec."Name" AS __text, ec."Quelle" AS __value
FROM "EnergyCommunities" ec
WHERE NOT ec."IsDeleted"
  AND NULLIF(ec."Quelle", '') IS NOT NULL
ORDER BY 1, 2`,
          centralApi, false),
        variable('communityMeters', 'Zählpunkte der Energiegemeinschaft',
          `SELECT mp.meter_point_number AS __text, mp.meter_point_number AS __value
FROM meter_point_communities mc
JOIN meter_points mp ON mp."Id" = mc.meter_id
WHERE mc.community_id = \${community:sqlstring}
UNION ALL
SELECT '__egwn_no_meter_points__', '__egwn_no_meter_points__'`,
          timescale, true, true, true),
        variable('meterPoint', 'Zählpunkt', centralSql, centralApi, false, true, true),
        variable('allMeters', 'Alle Zählpunkte dieser Richtung', centralSql, centralApi, true, true, true)
      ]
    }
  };
}

for (const [direction, file] of [
  ['consumption', 'meterpoints-bezug.json'],
  ['generation', 'meterpoints-einspeisung.json']
]) {
  fs.writeFileSync(path.join(__dirname, 'dashboards', file),
    JSON.stringify(dashboard(direction), null, 2) + '\n');
}
