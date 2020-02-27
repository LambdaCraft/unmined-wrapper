function getScripts(server, dimension, callback) {
    console.log("loading scripts");
    let scripts = [
        server + "/" + dimension + '/lambda.markers.js',
        server + "/" + dimension + '/lambda.players.js',
        server + "/" + dimension + '/unmined.map.properties.js',
        server + "/" + dimension + '/unmined.map.regions.js'
    ];
    var progress = 0;
    scripts.forEach(function(script) {
        $.getScript(script, function () {
            if (++progress === scripts.length) {
                callback();
            }
        });
    });
}