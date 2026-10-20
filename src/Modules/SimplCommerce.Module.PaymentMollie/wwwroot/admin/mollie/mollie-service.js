/*global angular*/
(function () {
    angular
        .module('simplAdmin.paymentMollie')
        .factory('paymentMollieService', ['$http', paymentMollieService]);

    function paymentMollieService($http) {
        var service = {
            getSettings: getSettings,
            updateSetting: updateSetting
        };
        return service;

        function getSettings() {
            return $http.get('api/mollie/config');
        }

        function updateSetting(settings) {
            return $http.put('api/mollie/config', settings);
        }
    }
})();
