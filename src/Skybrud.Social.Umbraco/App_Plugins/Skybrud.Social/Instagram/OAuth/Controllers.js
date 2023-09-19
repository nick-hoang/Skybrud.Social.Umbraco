angular.module("umbraco").controller("Skybrud.Social.Instagram.OAuth.Controller", ['$scope', 'editorState', '$http', function ($scope, editorState, $http) {

    // Define an alias for the editor (eg. used for callbacks)
    var alias = ('skybrudsocial_' + Math.random()).replace('.', '');

    // Get a reference to the current editor state        
    var state = editorState.current;

    $scope.loading = false;
    $scope.tmpHashtag = typeof $scope.model.value !== 'undefined' ? $scope.model.value.hashtag : '';
    $scope.mediaByHashtag = null;

    //get contentType config
    var currentPropery = state.properties.filter(e => e.alias == $scope.model.alias)[0];
    $scope.needprofessionalaccount = currentPropery.config.config.needprofessionalaccount;

    $scope.callback = function (data) {
        $scope.$apply(function () {
            $scope.model.value = data;
        });
    };

    $scope.authorize = function () {

        var url = '/App_Plugins/Skybrud.Social/Dialogs/InstagramOAuth.aspx?callback=' + alias;
        url += "&contentTypeAlias=" + state.contentTypeAlias;
        url += "&propertyAlias=" + $scope.model.alias;

        window.open(url, 'Instagram OAuth', 'scrollbars=no,resizable=yes,menubar=no,width=800,height=600');

    };

    $scope.clear = function () {
        $scope.model.value = null;
    };

    $scope.hashtagEnterKeypress = function (event) {
        if (event.which === 13 && !$scope.loading) {
            event.preventDefault();
            $scope.checkHashtagAvailability();
        }
    };

    $scope.checkHashtagAvailability = function () {
        if ($scope.tmpHashtag && $scope.tmpHashtag !== '') {
            $scope.loading = true;

            $scope.mediaByHashtag = null;
            $scope.model.value.hashtag = '';
            $scope.model.value.hashtagId = '';
            $http.get('https://graph.facebook.com/v18.0/ig_hashtag_search?user_id='
                + $scope.model.value.businessid + '&q=' + $scope.tmpHashtag
                + '&access_token=' + $scope.model.value.accessToken)
                .then(function (res) {
                    $scope.loading = false;
                    if (res.data.length === 0) {
                        alert('#' + $scope.tmpHashtag + ' is not available, please try another one');
                    }
                    else {
                        $scope.model.value.hashtag = $scope.tmpHashtag;
                        $scope.model.value.hashtagId = res.data.data[0].id;
                        $scope.loading = true;
                        $http.get('https://graph.facebook.com/' + $scope.model.value.hashtagId + '/recent_media?user_id=' + $scope.model.value.businessid
                            + '&fields=media_type,media_url,permalink'
                            + '&access_token=' + $scope.model.value.accessToken)
                            .then(function (res) {
                                $scope.loading = false;
                                $scope.mediaByHashtag = res.data.data.filter(e => e.media_type === 'IMAGE');
                            }, function (error) { $scope.loading = false; alert(error.data.error.message); console.log(error); });
                    }
                }, function (error) { $scope.loading = false; alert('#' + $scope.tmpHashtag + ' is not available, please try another one'); console.log(error); });
        }
        else {
            alert("Please input hashtag name");
        }
    };

    $scope.clearHashtag = function () {
        $scope.mediaByHashtag = null;
        $scope.model.value.hashtag = '';
        $scope.model.value.hashtagId = '';
        $scope.tmpHashtag = '';
    };

    // Register the callback function in the global scope
    window[alias] = $scope.callback;

}]);

angular.module("umbraco").controller("Skybrud.Social.Instagram.OAuth.PreValues.Controller", ['$scope', 'assetsService', function ($scope, assetsService) {

    if (!$scope.model.value) {
        $scope.model.value = {
            clientid: '',
            clientsecret: '',
            redirecturi: '',
            scope: ''
        };
    }

    $scope.scopes = ['user_profile', 'user_media'];

    $scope.professionalScopes = ['instagram_basic', 'pages_show_list'];

    $scope.updateScope = function () {
        if (typeof $scope.model.value.needprofessionalaccount === 'undefined') {
            $scope.model.value.needprofessionalaccount = false;
        }
        if ($scope.model.value.needprofessionalaccount) {
            $scope.model.value.scope = $scope.professionalScopes.join(',');
        }
        else {
            $scope.model.value.scope = $scope.scopes.join(',');
        }
    };

    $scope.init = function () {
        $scope.updateScope();
    };

    $scope.suggestedRedirectUri = window.location.origin + '/App_Plugins/Skybrud.Social/Dialogs/InstagramOAuth.aspx';

    $scope.init();

}]);