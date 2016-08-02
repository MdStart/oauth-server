﻿import {Component, Input} from "@angular/core";
import {ActivatedRoute} from "@angular/router";

import { UserService }   from '../user.service';
import {UserModel} from '../user.model';


@Component({
    templateUrl: './app/users/user-edit/user-edit.html'
})

export class UserEditComponent {
    user: UserModel;
    id: any;
    errorMessage: string;

    constructor(private userService: UserService, private route: ActivatedRoute) {
        this.user = new UserModel();
    }

    ngOnInit() {
        this.id = this.route.params.subscribe(params => {
            let id = this.route.snapshot.params['id'];

            this.userService.getUserById(id)
                .subscribe(
                user => this.user = user,
                error => this.errorMessage = <any>error)
        });
    }


    editUser(user: UserModel) {
        this.userService.editUser(user).subscribe((user) => {
            this.user = user;
        }, err => {
        });
    }

}

