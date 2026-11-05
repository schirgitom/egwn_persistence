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
Grafana benötigt Leserechte auf `meter_points`, `meter_point_communities`,
`raw_measurements` sowie auf `EnergyCommunities`, `MeteringPoints`,
`Customers` und `CustomerMeteringPoints` in der CentralAPI-Datenbank.

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
