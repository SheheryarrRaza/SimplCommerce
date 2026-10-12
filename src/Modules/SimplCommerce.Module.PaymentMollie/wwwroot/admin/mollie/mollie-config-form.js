/*global angular, jQuery*/
(function ($) {
    angular
        .module('simplAdmin.paymentMollie')
        .controller('MollieConfigFormCtrl', ['paymentMollieService', 'translateService', MollieConfigFormCtrl]);

    function MollieConfigFormCtrl(paymentMollieService, translateService) {
        var vm = this;
        vm.translate = translateService;
        vm.mollieConfig = {};

        vm.save = function save() {
            vm.validationErrors = [];
            paymentMollieService.updateSetting(vm.mollieConfig)
                .then(function (result) {
                    toastr.success('Mollie settings have been saved');
                })
                .catch(function (response) {
                    var error = response.data;
                    vm.validationErrors = [];
                    if (error && angular.isObject(error)) {
                        for (var key in error) {
                            vm.validationErrors.push(error[key][0]);
                        }
                    } else {
                        vm.validationErrors.push('Could not save Mollie settings.');
                    }
                });
        };

        function init() {
            paymentMollieService.getSettings().then(function (result) {
                vm.mollieConfig = result.data;
            });
        }

        init();
    }
})(jQuery);
