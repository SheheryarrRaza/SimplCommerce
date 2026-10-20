/*global angular, jQuery, toastr*/
(function ($) {
    angular
        .module('simplAdmin.paymentMollie')
        .controller('MollieConfigFormCtrl', ['paymentMollieService', 'translateService', MollieConfigFormCtrl]);

    function MollieConfigFormCtrl(paymentMollieService, translateService) {
        var vm = this;
        vm.translate = translateService;
        vm.mollieConfig = {};
        vm.validationErrors = [];
        vm.loadFailed = false;

        vm.save = function save() {
            if (vm.loadFailed) {
                return;
            }
            vm.validationErrors = [];
            paymentMollieService.updateSetting(vm.mollieConfig)
                .then(function () {
                    toastr.success('Application settings have been saved');
                })
                .catch(function (response) {
                    var error = response.data;
                    vm.validationErrors = [];
                    if (error && angular.isObject(error)) {
                        for (var key in error) {
                            vm.validationErrors.push(error[key][0]);
                        }
                    } else {
                        vm.validationErrors.push('Could not save settings.');
                    }
                });
        };

        function init() {
            paymentMollieService.getSettings().then(function (result) {
                vm.mollieConfig = result.data;
            }).catch(function () {
                vm.loadFailed = true;
                vm.validationErrors.push('Could not load settings. Reload the page before saving.');
            });
        }

        init();
    }
})(jQuery);
