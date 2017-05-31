﻿import * as React from "react";
import { FormField } from "./../restObjects/FormField";
import { FormFieldType } from "./../restObjects/FormFieldType";


interface FormFieldComponentProps {
    field: FormField;
}

export class FormFieldComponent extends React.Component<FormFieldComponentProps, any>{

    
    public constructor(props: FormFieldComponentProps) {
        super(props);
        let currentVal 
        this.state = undefined;
        this.handleChange = (event) => {
            this.setState((prevState, props) => {
                return event.currentTarget.value; 
            });
        };
    }


    private mapFieldTypeToInputType = new Map<string, string>(
        [
            ["String", "text"],
            ["Date", "date"],
            ["Boolean", "checkbox"]
        ]
    );

    public readonly handleChange: React.EventHandler<React.FormEvent<HTMLInputElement>>;

    public render() {
        let f = this.props.field;
        let attributes: React.HTMLAttributes<HTMLInputElement> = {
            type: this.mapFieldTypeToInputType.get(f.type) || "text",
            className: "form-control",
            id: f.name,
            name: f.name,
            minLength: f.minLength,
            maxLength: f.maxLength,
            title: f.description,
            placeholder: f.placeholder,
            value: this.state,
            required: f.required,
            pattern: f.pattern
        };

        let input = f.type !== "Boolean"
            ? < div className="form-group" >
                <label htmlFor={f.name}>{f.label}{f.required ? <span className="text-danger">*</span> : null}</label>
                <input ref={f.name} {...attributes} onChange={this.handleChange} />
            </div>
            : < div className="form-group" >
                <input ref={f.name} {...attributes} onChange={this.handleChange} />
                <label htmlFor={f.name}>{f.label}</label>
            </div>;


        return input;
    }
}