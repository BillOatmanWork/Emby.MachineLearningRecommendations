define(["loading", "dialogHelper", "mainTabsManager", "formDialogStyle"],
    function (loading, dialogHelper, mainTabsManager) {

        var pluginId = "ABDD70A3-B516-4E35-A0D1-C6447BEBB8BA";
        var data = [];
        function getTabs() {
            return [
                {
                    href: Dashboard.getConfigurationPageUrl(''),
                    name: ""
                }
            ];
        }
        async function getUsers() {
            var result = await ApiClient.getJSON(ApiClient.getUrl("/Users"));
            return result;
        }
        async function getMovies(userId) {
            var result = await ApiClient.getJSON(ApiClient.getUrl("/Items?IncludeItemTypes=Movie&Recursive=true&UserId=" + userId));
            return result;
        }

        function getRandomMovie(movies) {
            return movies[Math.round(Math.random() * movies.length)];
        }
        // once we have a good set of data, generate some color combinations!
        function predictMovie(brain, movies) {
            //const data = JSON.parse(window.localStorage.trainingData);
            if (!data.length) {
                console.log('no training Data');
                return;
            }
            const net = new brain.NeuralNetwork({ activation: "leaky-relu", hiddenLayers: [100] });
            var results = [];

            console.log("Training Neural Network");
            net.train(data);
            console.log("Neural Network Training Complete...");
            
            for (let i = 0; i <= 10000; i++) {
                var randomMovieOne = getRandomMovie(movies.Items);
                var randomMovieTwo = getRandomMovie(movies.Items);
                var randomMovieThree = getRandomMovie(movies.Items);
                var list = [randomMovieOne, randomMovieTwo, randomMovieThree];
                const [score] = net.run(list);
                results.push({ randomMovieOne, randomMovieTwo, randomMovieThree, score });
                console.log("Random Movie Neuron added");
            }
                                
            // sort results
            const sortedResults = results.sort(function(a, b) {
                var a = a.score;
                var b = b.score;

                return b - a;
            });

            // keep the top 20 results!
            for (let i = 0; i < 20; i++) {
                console.log(sortedResults[i]);
            }
            data = [];
            results = [];
        }
        return function (view) {
            view.addEventListener('viewshow',
                async () => {

                    //loading.show();

                    //mainTabsManager.setTabs(this, 0, getTabs);

                    var config = await ApiClient.getPluginConfiguration(pluginId);
                    var users = await getUsers();
                    var user = users.filter(u => u.Policy.IsAdministrator);
                    var userId = user[0].Id;
                    var movies = await getMovies(userId);
                    for (let i = 0; i <= movies.Items.length-2; i++) {
                        var out;
                        if (movies.Items[i].UserData.IsFavorite &&
                            movies.Items[i + 1].UserData.IsFavorite) {
                            out = 1;
                        } else {
                            out = 0;
                        }
                        data.push({
                            input: [movies.Items[i], movies.Items[i+1]],
                            output: [out]
                        });

                    }
                    require([Dashboard.getConfigurationResourceUrl('brain')],
                        async (brain) => {
                            //window.localStorage.trainingData = window.localStorage.trainingData || JSON.stringify([]); 
                            predictMovie(brain, movies);
                        });
                });

            //});


            view.addEventListener('viewhide',
                async () => {

                });
        }

    });