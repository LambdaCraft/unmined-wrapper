function UpdateMap(map, layersGroup) {

	layersGroup.clearLayers();

	console.log("updating lambda pois");

	LambdaPOIs.forEach(function (poi){
		var newC = map.unproject(poi.center, map.getMaxZoom());

		var marker = L.shapeMarker(
				newC, poi.properties
			);

		if (null != poi.tooltip) {
			marker.bindTooltip(poi.tooltip);
		}

		layersGroup.addLayer(marker);

	} );

	LambdaInfra.forEach(function (poi) {
		var newC = [];
		poi.coords.forEach(function(c) {
			newC.push(map.unproject(c, map.getMaxZoom()));
		});

		var line = L.polyline(newC, poi.properties);

		if (null != poi.tooltip) {
			line.bindTooltip(poi.tooltip);
		}

		layersGroup.addLayer(line);
	});

	let list = document.getElementById('playersOnline');
	list.innerHTML="";

	LambdaPlayers.forEach(function (player) {

		var icon = L.icon({
			iconUrl: '../portraits/' + player["name"] + ".png", 
			iconSize: [12, 12],
		});

		var newC = map.unproject([player["x"], player["z"]], map.getMaxZoom());
		var marker = L.marker(newC, {icon: icon});
		var name = "";
		if (player["bot"]) {
			name = "[Bot] ";
		}

		name += player["name"];
		marker.bindTooltip(name);

		layersGroup.addLayer(marker);

		let li = document.createElement("li");
		li.innerHTML = name;
		li.onclick = function() {
			map.setView(marker.getLatLng(), map.getZoom());
		};
		list.appendChild(li);
	});
}
