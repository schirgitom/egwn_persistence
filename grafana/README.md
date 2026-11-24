# Zählpunkt-Dashboards

`dashboards/meterpoints-bezug.json` und `dashboards/meterpoints-einspeisung.json`
sind Grafana-Dashboards im v2-Format. Sie verwenden die PostgreSQL-Datenquellen
`bfywptosighs0b` (EGWNPersistence/TimescaleDB) und `bfz0tx8cvxszkc`
(EGWNCentralAPI). Falls diese UIDs in einer anderen Grafana-Instanz abweichen,
vor dem Import beide Datenquellen in den JSON-Dateien ersetzen.
Nach Änderungen an den JSON-Dateien das jeweilige Dashboard in Grafana erneut
importieren und die vorhandene Version überschreiben; Grafana liest die Datei
nicht automatisch erneut ein.

Die Community-Auswahl stammt aus `EnergyCommunities` der CentralAPI-Datenbank:
`Name` ist die Beschriftung und `Quelle` die externe Community-ID, die in
`meter_point_communities.community_id` und `raw_measurements.community_id`
gespeichert wird. Nur nicht gelöschte Gemeinschaften mit befüllter `Quelle`
werden angeboten. Die versteckte Variable `communityMeters` stammt aus
`meter_point_communities` in der Persistence-Datenbank. Die sichtbare
Zählpunktauswahl aus der CentralAPI-Datenbank zeigt nur aktive Zählpunkte der
jeweiligen Energierichtung, für die in Persistence eine Zuordnung zur gewählten
Community erfasst ist; Kundennamen bleiben als Beschriftung erhalten. Dies
ist keine Prüfung der aktuellen CentralAPI-Teilnahmen. Beide Datenbanken sind nur über den externen
Zählpunktcode verbunden. Die Messwerte werden immer auf **eine** ausgewählte
Community eingeschränkt. Der Nenner des Panels „Anteil an allen …“ umfasst
nur aktive Zählpunkte der jeweiligen Richtung, für die in dieser Community
Messwerte vorliegen.
Die bisherige Dashboard-Konfiguration benötigt Leserechte auf `meter_points`,
`meter_point_communities` und `raw_measurements` sowie auf `EnergyCommunities`,
`MeteringPoints`, `Customers` und `CustomerMeteringPoints` in der
CentralAPI-Datenbank. **Diese direkten Tabellenrechte sind nicht für
kundenspezifische Grafana-Datenquellen geeignet.** Siehe unten für die
mandantenbegrenzten Views; die Dashboard-Abfragen müssen dafür angepasst werden.

## Kundenspezifischer Persistence-Zugriff

Nach den EF-Migrationen `db/grafana_customer_access.sql` einmalig mit einem
administrativen DB-Zugang ausführen. **Nicht** als Grafana-Login oder mit dem
Worker-Zugang ausführen. Dieser Zugang muss Rollen und Schemas anlegen können
und Eigentümer der Views sowie der Tabellen im Schema `grafana_access` sein.
Die Installation ist unabhängig von EF und wird nicht automatisch beim
Worker-Start ausgeführt. Vor dem Anlegen von Kunden prüfen, dass `PUBLIC` und
Grafana-Logins keine direkten `SELECT`-Rechte auf `public`-Basistabellen und
Rollups besitzen. Die Provisionierungsfunktion verweigert Kunden mit solchen
Rechten; später vergebene globale Rechte müssen ebenfalls vermieden werden.

Die **ASP.NET-Core-Web-API** mit `GrafanaCustomersController` läuft im
bestehenden Persistence-Prozess neben den RabbitMQ-Consumern. Standardmäßig
ist sie deaktiviert: ohne
`Provisioning__Enabled=true` öffnet der Worker keinen HTTP-Port. Für die
Aktivierung `Provisioning__ConnectionString` (separater DB-Zugang) und
`Provisioning__ServiceToken` (zufälliges Secret mit mindestens 32 Zeichen)
aus einem Secret-Store injizieren. Der DB-Zugang für den Messwert-Worker
(`Database__ConnectionString`) bleibt davon getrennt und erhält **keine**
Provisionierungsrechte. Der DBA legt den DB-Login für die Provisionierung
mit `CREATEROLE` an; dafür ist ein dauerhaftes, eng abgesichertes
Servicekonto erforderlich. Nach Installation des Skripts braucht dieses
Konto außerdem folgende Rechte (Namen/DB anpassen):

```sql
GRANT CREATE ON DATABASE egwndata TO provisioning_api_role;
GRANT USAGE ON SCHEMA grafana_access TO provisioning_api_role;
GRANT SELECT, INSERT ON grafana_access.customers TO provisioning_api_role;
GRANT SELECT, INSERT, DELETE ON grafana_access.communities, grafana_access.meters TO provisioning_api_role;
GRANT SELECT ON public.meter_points, public.meter_point_communities,
    public.raw_measurements, public.kpi_measurements TO provisioning_api_role;
GRANT EXECUTE ON FUNCTION grafana_access.provision_customer(uuid, text),
    grafana_access.install_community_hourly_view(uuid, name, name),
    grafana_access.replace_entitlements(uuid, text[], jsonb),
    grafana_access.disable_customer(uuid) TO provisioning_api_role;
```

Diese Rechte sind **nur** für das interne API-DB-Konto, nie für
kundenspezifische Datenbankbenutzer. Ein Wechsel dieses Kontos erfordert
separate Übertragung der Verwaltungsrechte auf bereits angelegte Kundenrollen.
Bei Aktivierung lauscht der Worker zusätzlich auf HTTPS-Port 8443
(`Provisioning__Port` kann ihn ändern). In Produktion ein für den
internen DNS-Namen des **Workers** gültiges TLS-Zertifikat konfigurieren,
z. B. per
`Kestrel__Certificates__Default__Path` und
`Kestrel__Certificates__Default__Password` aus Secrets. Den Port nur im
internen Netz zum Grafana Interface freigeben, nicht öffentlich exponieren.
Der Worker installiert das SQL-Skript **nicht** selbst: Auf dem Server nach
Änderungen an `db/grafana_customer_access.sql` erneut ausführen. Das
aktualisierte Worker-Image deployen; keine zweite API deployen. Bei
Docker das Zertifikat nur lesbar in den Worker-Container mounten und Port
8443 ausschließlich intern erreichbar machen. Das Grafana Interface
verwendet z. B. `https://egwnpersistence-worker:8443`.

Das Grafana Interface authentifiziert **jeden** Aufruf mit
`Authorization: Bearer <service-token>` über HTTPS; Zertifikat
und DNS-Namen normal prüfen (kein unsicherer TLS-Modus). Es muss die
Kunden-/Teilnahme-Zuordnung aus einem autoritativen System beziehen.
Vertrag (nur nach autorisiertem Onboarding, mit dauerhaft stabiler UUID):

1. `POST /internal/grafana/customers/{customerId}/credentials` ohne Body.
   Antwort `200` mit
   `{"username":"grafana_customer_<uuid_ohne_bindestriche>",
   "schema":"grafana_customer_<uuid_ohne_bindestriche>","password":"..."}`.
   Das Passwort wird kryptographisch zufällig erzeugt und **nur in dieser
   Antwort** ausgegeben; kein GET-Endpunkt für Secrets. Jeder erneute POST
   **rotiert** das Passwort, auch bei Retry nach einem Timeout. Danach das
   zurückgegebene Secret atomar in der Grafana-Datenquelle hinterlegen; ein
   verlorenes Passwort mit erneutem POST rotieren. Die DB-Funktion vergibt
   noch keine Datenrechte, solange Schritt 2 nicht erfolgt.
2. `PUT /internal/grafana/customers/{customerId}/entitlements` mit
   `Content-Type: application/json` und dem **vollständigen** aktuellen
   Berechtigungsstand, z. B.
   `{"communities":["community-1","community-2"],"meters":
   [{"communityId":"community-1","meterPointNumber":"AT001"}]}`.
   Antwort `204`. Die Liste wird atomar ersetzt: Ein Paar berechtigt nur
   genau diesen Zählpunkt **in genau dieser Community**. Für dieselbe
   Zählpunktnummer in einer weiteren Community braucht es ein zweites Paar.
   Leere Arrays entziehen alle Datenrechte; bei jeder Teilnahmeänderung
   erneut die vollständigen Listen senden. Unbekannte Kunden liefern `404`,
   DB-Fehler `500` statt einer Erfolgsmeldung.
3. Mit dem Login aus Schritt 1 in der richtigen Grafana-Organisation eine
   PostgreSQL-Datenquelle einrichten und erst nach erfolgreichem Zugriffstest
   freigeben. Die Dashboard-SQL muss auf das feste, vom Service aus der
   Kunden-UUID berechnete Schema qualifizieren (z. B.
   `grafana_customer_<uuid_ohne_bindestriche>.raw_measurements`), nicht auf
   `public` und nicht auf eine durch Nutzer veränderbare Schema-Variable.
   Bestehende Dashboards benötigen diese Anpassung; keine globale Datenquelle
   als Fallback einsetzen.
4. Bei Kündigung `POST /internal/grafana/customers/{customerId}/disable`
   aufrufen (Antwort `204`) und die Grafana-Datenquelle entfernen. Das sperrt neue DB-Anmeldungen und
   entzieht allen Views ihre Berechtigungszeilen (auch bestehenden Sessions).
   Erneuter `/credentials`-Aufruf aktiviert den Login mit neuem Passwort;
   Berechtigungen müssen separat wiederhergestellt werden.

Der API-Token berechtigt zur Provisionierung **aller Kunden** und darf nur
beim Grafana Interface liegen. API-Zugriffs- und Proxy-Logs dürfen weder
Authorization-Header noch Antwort-Body protokollieren. Auch Grafana-Dashboards
dürfen keine Passwörter enthalten. Der Zugriff auf CentralAPI erfordert eine
eigene, separat begrenzte Datenquelle.
Der API-Host verwendet dieselbe Serilog-Konfiguration wie der Worker und
protokolliert je HTTPS-Anfrage einen Eintrag mit Methode, Pfad, Status und Dauer;
Header sowie Anfrage- und Antwort-Body werden dabei nicht protokolliert.

Alle fünf Views sind `security_barrier`-Views; der Login hat nur `USAGE` auf
sein Schema und `SELECT` auf dessen Views, **keinen direkten Zugriff** auf die
Basistabellen oder globale Timescale-Rollups:

| View | Felder | Sichtbare Daten |
|---|---|---|
| `raw_measurements` | `meter_id`, `community_id`, `timestamp`, `total_consumption`, `grid_share`, `community_share` | Nur freigegebene Community-Zählpunkt-Paare mit tatsächlicher Community-Mitgliedschaft |
| `meter_points` | `"Id"`, `meter_point_number`, `energy_direction` (nullable) | Nur freigegebene Zählpunkte mit tatsächlicher Community-Mitgliedschaft |
| `meter_point_communities` | `meter_id`, `community_id` | Nur freigegebene Paare mit tatsächlicher Community-Mitgliedschaft |
| `kpi_measurements` | `community_id`, `timestamp`, `autarky`, `own_consumption`, `community`, `feed`, `remaining_demand` | Community-KPIs aller freigegebenen Communities (auch ohne eigene Zählpunktdaten) |
| `community_hourly_measurements` | `community_id`, `energy_direction`, `timestamp`, `total_consumption`, `grid_share`, `community_share` | Stundensummen **aller** Mitglieder freigegebener Communities, nach `consumption`/`generation` getrennt; keine Zählpunktkennung |

Die `meter_points`-View enthält keine Community-ID; ihre Zählpunkte können
mehreren Communities zugeordnet sein. Messwert- und
`meter_point_communities`-Abfragen müssen deshalb immer beide IDs verwenden.
Die neue Stunden-View wird beim erneuten Ausführen von
`db/grafana_customer_access.sql` auch für **bereits provisionierte Kunden**
angelegt, ohne deren Passwörter zu rotieren. Für neu angelegte Kunden erstellt
die Provisionierungsfunktion sie automatisch. Werte in kWh lassen sich über
Grafana-Zeitintervalle summieren; die gewichtete EG-Quote ist
`SUM(community_share) / NULLIF(SUM(total_consumption), 0)`. Für den Verlauf
von Netzbezug und EG-Bezug (Schema je Kunde fest konfigurieren):

```sql
SELECT $__timeGroupAlias(timestamp, $__interval),
       SUM(grid_share) AS "Netzbezug",
       SUM(community_share) AS "Bezug aus EG"
FROM grafana_customer_<uuid_ohne_bindestriche>.community_hourly_measurements
WHERE $__timeFilter(timestamp)
  AND community_id = ${community:sqlstring}
  AND energy_direction = 'consumption'
GROUP BY 1 ORDER BY 1;
```

Für Erzeuger `energy_direction = 'generation'` verwenden; der Spitzenwert in
kW ergibt sich aus dem Maximum der stündlichen `total_consumption` (kWh).
Messwerte ohne `energy_direction` werden nicht in die Richtungssummen
eingerechnet. Da die Richtung am Zählpunkt gespeichert wird, wirkt eine
nachträgliche Richtungsänderung auch auf historische Summen. Die View enthält
bewusst auch Messwerte **fremder** Teilnehmer derselben Gemeinschaft; bei
kleinen Gemeinschaften können Summen Rückschlüsse auf Einzelwerte ermöglichen.
Für Einzelmesswerte weiterhin ausschließlich `raw_measurements` verwenden.
Community-übergreifende Rollups haben keine kundenspezifischen Views und sind
gesperrt. Ebenso benötigt die **CentralAPI-Datenbank** eine separate
kundenspezifische Datenquelle und begrenzte Views für Community-Namen,
Zählpunkte und Kundennamen. Dieses Repository kann deren Berechtigungen nicht
vergeben. Die vorhandenen JSON-Dashboards sind bis zu dieser Umstellung
**nicht für Kunden freigabefähig**; insbesondere `meterpoint-details.json`
enthält Abfragen ohne Community-Filter.

Der Messwert-Producer kann im RabbitMQ-Payload zusätzlich auf oberster Ebene
`energyDirection` mit `consumption` oder `generation` senden. Das Feld
`meter_points.energy_direction` ist nullable; bei bestehenden Zählpunkten
wird es bei jedem gelieferten Wert aktualisiert, auch wenn der Messwert
bereits vorhanden ist. Fehlt das Feld, bleibt der gespeicherte Wert unverändert.
Die Worker-Logs zeigen bei verarbeiteten Nachrichten sowohl die eingehende
Richtung als auch die nach dem Commit gespeicherte Richtung; `null` als
eingehender Wert bedeutet, dass die Nachricht keine Richtung geliefert hat.
Die hier versionierten Zählpunkt-Dashboards verwenden weiterhin
`MeteringPoints.EnergyDirection` aus CentralAPI zur Richtungsfilterung.

Die Persistence speichert **Stundenwerte**, keine Viertelstundenwerte.
Stündliche kWh entsprechen der durchschnittlichen Leistung in kW innerhalb
dieser Stunde. Tagesprofile und Vollständigkeit benutzen Wiener Ortszeit;
bei der Winterzeitumstellung werden die beiden gleichen Ortsstunden im
Stundenprofil gemittelt und in der Tageszeitdarstellung summiert. Fehlende
Stunden werden nicht durch Nullwerte ersetzt. Bei mehr als zwei Jahren
Rohdaten-Retention können die Ansichten keine älteren Zeiträume anzeigen.
Der Vorwochenvergleich benötigt zusätzlich die sieben vorangegangenen Tage.

Nach Änderungen an `generate-meterpoint-dashboards.js` beide JSON-Dateien mit
`node grafana\generate-meterpoint-dashboards.js` neu erzeugen.
